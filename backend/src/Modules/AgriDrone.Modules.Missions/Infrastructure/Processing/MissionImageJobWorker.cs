using AgriDrone.Modules.Missions.Application.Features.Processing;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class MissionImageJobWorker(
    IServiceScopeFactory scopes, TimeProvider clock, ILogger<MissionImageJobWorker> logger) : BackgroundService
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
                logger.LogError(ex, "Unable to process the mission image job queue.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MissionsDbContext>();
        var jobs = scope.ServiceProvider.GetRequiredService<MissionImageJobStore>();
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

                if (job.Kind == MissionImageJobStore.Disease)
                {
                    var handled = false;
                    string? diseaseError = null;
                    try
                    {
                        (handled, diseaseError) = await scope.ServiceProvider
                            .GetRequiredService<IMissionDiseaseJobProcessor>()
                            .ExecuteJobAsync(job, runId, token);
                        token.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        logger.LogError(ex, "Image job {JobId} ({Kind}) failed.", job.Id, job.Kind);
                        diseaseError = $"DetectDiseases.Failed: {ex.Message}";
                    }

                    if (!handled)
                    {
                        await jobs.FinishAsync(
                            job.Id, runId, null,
                            diseaseError ?? "Disease detection failed. Verify FastAPI availability and frozen inputs, then retry.",
                            clock.GetUtcNow(), token);
                    }

                    return true;
                }

                string? output = null;
                try
                {
                    var service = scope.ServiceProvider.GetRequiredService<IMetashapeService>();
                    output = job.Kind switch
                    {
                        MissionImageJobStore.Tiling => await SliceTilesAsync(service, job, runId, token),
                        MissionImageJobStore.Yolo => await DetectTreesAsync(service, job, token),
                        _ => throw new InvalidOperationException($"Unknown image job kind '{job.Kind}'.")
                    };
                    token.ThrowIfCancellationRequested();
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    logger.LogError(ex, "Image job {JobId} ({Kind}) failed.", job.Id, job.Kind);
                }

                var error = job.Kind == MissionImageJobStore.Yolo
                    ? "YOLO failed. Verify FastAPI availability and the selected tiling result, then retry."
                    : "Tiling failed. Verify the GeoTIFF and clipping area, then retry.";
                await jobs.FinishAsync(job.Id, runId, output, output is null ? error : null, clock.GetUtcNow(), token);
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

    private async Task<string?> SliceTilesAsync(IMetashapeService service, MissionImageJobStore.Job job,
        Guid runId, CancellationToken token)
    {
        var input = job.ReadInput<MissionTilingInput>();
        var result = await service.SliceTilesAsync(job.MissionId, job.TenantId, input.SourceKey,
            input.TileSize, input.Overlap, input.DualScale, input.PolygonGeoJson,
            uploadToMinio: true, tilingJobId: job.Id, tilingRunId: runId,
            sourceSha256: input.SourceChecksum is { Length: 64 } checksum && checksum.All(Uri.IsHexDigit) ? checksum : null,
            cancellationToken: token);
        var prefix = $"tenants/{job.TenantId:D}/media/{job.MissionId:D}/tiling-jobs/{job.Id:D}/{runId:D}";
        var success = result is { IsSuccess: true, TotalTiles: > 0, StorageBucket.Length: > 0 } &&
            result.MissionId == job.MissionId && result.ManifestStorageKey == $"{prefix}/manifest.json" &&
            result.TotalTiles == result.Tiles.Count &&
            result.Tiles.Select(tile => tile.FileName).Distinct(StringComparer.Ordinal).Count() == result.TotalTiles &&
            result.Tiles.All(tile => tile.FileName.Length > 0 && tile.FileName == Path.GetFileName(tile.FileName) &&
                tile.FileName is not ("." or "..") && tile.StorageKey == $"{prefix}/tiles/{tile.FileName}");
        if (!success)
            logger.LogWarning("Tiling job {JobId} produced no usable result: {Error}", job.Id, result.ErrorMessage);
        return success ? JsonSerializer.Serialize(result) : null;
    }

    private async Task<string?> DetectTreesAsync(IMetashapeService service, MissionImageJobStore.Job job,
        CancellationToken token)
    {
        var input = job.ReadInput<MissionYoloInput>();
        var result = await service.DetectMissionTreesAsync(job.MissionId, job.TenantId,
            input.ConfidenceThreshold, input.RowSpacing, input.ColSpacing, input.PolygonGeoJson,
            input.AnchorLat, input.AnchorLon, input.CustomAngleDeg,
            tileManifestStorageKey: input.ManifestStorageKey, cancellationToken: token);

        if (!result.IsSuccess || result.MissionId != job.MissionId || result.TotalTilesScanned != input.TotalTiles)
        {
            logger.LogWarning("YOLO job {JobId} produced no usable result. Expected {Expected} tiles, scanned {Actual}. {Error}",
                job.Id, input.TotalTiles, result.TotalTilesScanned, result.ErrorMessage);
            return null;
        }

        return JsonSerializer.Serialize(result);
    }
}
