using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class MissionTilingWorker(
    IServiceScopeFactory scopes, TimeProvider clock, ILogger<MissionTilingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessOneAsync(stoppingToken))
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to process the tiling queue.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MissionsDbContext>();
        var jobs = scope.ServiceProvider.GetRequiredService<MissionTilingJobStore>();
        var pending = await jobs.ReadPendingAsync(token);
        if (pending.Count == 0)
            return false;

        // Keep the owning PostgreSQL session open until FastAPI finishes.
        await db.Database.OpenConnectionAsync(token);
        foreach (var job in pending)
        {
            if (!await jobs.TryLockAsync(job.Id, token))
                continue;

            try
            {
                var runId = Guid.CreateVersion7();
                if (await jobs.StartAsync(job.Id, runId, clock.GetUtcNow(), token) == 0)
                    continue;

                SliceTilesResult? result = null;
                try
                {
                    var input = job.ReadInput();
                    var service = scope.ServiceProvider.GetRequiredService<IMetashapeService>();
                    result = await service.SliceTilesAsync(job.MissionId, job.TenantId, input.SourceKey,
                        input.TileSize, input.Overlap, input.DualScale, input.PolygonGeoJson,
                        uploadToMinio: true, tilingJobId: job.Id, tilingRunId: runId,
                        sourceSha256: input.SourceChecksum is { Length: 64 } checksum && checksum.All(Uri.IsHexDigit) ? checksum : null,
                        cancellationToken: token);
                    token.ThrowIfCancellationRequested();
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    logger.LogError(ex, "Tiling job {JobId} failed.", job.Id);
                }

                var prefix = $"tenants/{job.TenantId:D}/media/{job.MissionId:D}/tiling-jobs/{job.Id:D}/{runId:D}";
                var success = result is { IsSuccess: true, TotalTiles: > 0, StorageBucket.Length: > 0 } &&
                    result.MissionId == job.MissionId && result.ManifestStorageKey == $"{prefix}/manifest.json" &&
                    result.TotalTiles == result.Tiles.Count &&
                    result.Tiles.Select(tile => tile.FileName).Distinct(StringComparer.Ordinal).Count() == result.TotalTiles &&
                    result.Tiles.All(tile => tile.FileName.Length > 0 && tile.FileName == Path.GetFileName(tile.FileName) &&
                        tile.FileName is not ("." or "..") && tile.StorageKey == $"{prefix}/tiles/{tile.FileName}");
                if (!success)
                    logger.LogWarning("Tiling job {JobId} produced no usable result: {Error}", job.Id, result?.ErrorMessage);

                await jobs.FinishAsync(job.Id, runId, success ? JsonSerializer.Serialize(result) : null,
                    success ? null : "Tiling failed. Verify the GeoTIFF and clipping area, then retry.",
                    clock.GetUtcNow(), token);
                return true;
            }
            finally
            {
                if (db.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                    await jobs.UnlockAsync(job.Id, CancellationToken.None);
            }
        }

        return false;
    }
}
