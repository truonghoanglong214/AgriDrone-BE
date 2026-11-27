using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class SliceMissionTilesCommandHandler(
    IDroneMissionRepository missions,
    MissionsDbContext db,
    MissionImageJobStore jobs,
    ISystemManagerAccessService managerAccess,
    TimeProvider clock)
    : IRequestHandler<SliceMissionTilesCommand, Result<MissionTilingJobResponse>>,
      IRequestHandler<GetMissionTilingJobQuery, Result<MissionTilingJobResponse>>
{
    public async Task<Result<MissionTilingJobResponse>> Handle(
        SliceMissionTilesCommand request, CancellationToken token)
    {
        if (!await CanAccessAsync(request.TenantId, request.FarmId, token))
            return Denied();

        if ((!request.DualScale && (request.TileSize is < 1 or > 4096 || request.Overlap < 0 ||
                                    request.Overlap >= request.TileSize)) ||
            request.PolygonGeoJson is { ValueKind: not (JsonValueKind.Object or JsonValueKind.Null) })
            return Result.Failure<MissionTilingJobResponse>(
                AppError.Validation("Tiling.InvalidInput", "Provide a GeoJSON object and a valid tile size and overlap."));

        if (await missions.GetByIdAsync(request.MissionId, request.TenantId, request.FarmId, token) is null)
            return Result.Failure<MissionTilingJobResponse>(MissionError.NotFound(request.MissionId));

        await using var tx = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT id FROM mission.drone_missions
            WHERE id = {request.MissionId} AND tenant_id = {request.TenantId} AND farm_id = {request.FarmId}
            FOR UPDATE
            """, token);

        var requestedKey = string.IsNullOrWhiteSpace(request.TifStorageKey) ? null : request.TifStorageKey;
        var source = await db.MissionMedia.AsNoTracking()
            .Where(link => link.MissionId == request.MissionId && link.MediaRole == MissionMediaRole.ProcessedImage &&
                link.Media.TenantId == request.TenantId && link.Media.FarmId == request.FarmId &&
                link.Media.MimeType == "image/tiff" && link.Media.StorageStatus == MediaStorageStatus.Active &&
                link.Media.DeletedAt == null && (requestedKey == null || link.Media.StorageKey == requestedKey))
            .OrderByDescending(link => link.CreatedAt).ThenByDescending(link => link.MediaId)
            .Select(link => new { link.Media.StorageKey, link.Media.Checksum }).FirstOrDefaultAsync(token);
        var defaultKey = $"tenants/{request.TenantId:D}/media/{request.MissionId:D}/orthophoto.tif";
        if (source is null && requestedKey is not null && requestedKey != defaultKey)
            return Result.Failure<MissionTilingJobResponse>(
                AppError.Validation("Tiling.InvalidSource", "Select a GeoTIFF that belongs to this mission."));

        var input = JsonSerializer.Serialize(new MissionTilingInput(
            source?.StorageKey ?? defaultKey, source?.Checksum,
            request.DualScale ? 640 : request.TileSize, request.DualScale ? 64 : request.Overlap,
            request.DualScale, request.PolygonGeoJson));

        var active = await jobs.FindActiveAsync(request.MissionId, MissionImageJobStore.Tiling, token);
        if (active is not null)
        {
            if (!await jobs.MatchesAsync(active.Id, input, token))
                return Result.Failure<MissionTilingJobResponse>(
                    AppError.Conflict("Tiling.JobAlreadyActive", "Wait for the current tiling job before changing its inputs."));

            await tx.CommitAsync(token);
            return Result.Success(active.ToResponse());
        }

        var id = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        await jobs.InsertAsync(id, request.TenantId, request.FarmId, request.MissionId, MissionImageJobStore.Tiling, input, now, token);
        await tx.CommitAsync(token);
        return Result.Success(new MissionTilingJobResponse(id, request.MissionId, "QUEUED", now, null, null,
            request.PolygonGeoJson, null, null));
    }

    public async Task<Result<MissionTilingJobResponse>> Handle(GetMissionTilingJobQuery request, CancellationToken token)
    {
        if (!await CanAccessAsync(request.TenantId, request.FarmId, token))
            return Denied();
        var job = await jobs.FindAsync(request.TenantId, request.FarmId, request.MissionId, MissionImageJobStore.Tiling, request.JobId, token);
        return job is null
            ? Result.Failure<MissionTilingJobResponse>(AppError.NotFound("Tiling.JobNotFound", "Tiling job was not found."))
            : Result.Success(job.ToResponse());
    }

    private async Task<bool> CanAccessAsync(Guid tenantId, Guid farmId, CancellationToken token)
    {
        var access = await managerAccess.ResolveFarmAccessAsync(farmId, token);
        return access.IsAllowed && access.TenantId == tenantId && access.FarmId == farmId;
    }

    private static Result<MissionTilingJobResponse> Denied() =>
        Result.Failure<MissionTilingJobResponse>(AppError.Forbidden("Tiling.FarmAccessDenied", "The manager is not assigned to this farm."));
}
