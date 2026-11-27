using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class DetectMissionTreesCommandHandler(
    MissionsDbContext db,
    MissionImageJobStore jobs,
    ISystemManagerAccessService managerAccess,
    TimeProvider clock)
    : IRequestHandler<DetectMissionTreesCommand, Result<MissionYoloJobResponse>>,
      IRequestHandler<GetMissionYoloJobQuery, Result<MissionYoloJobResponse>>
{
    public async Task<Result<MissionYoloJobResponse>> Handle(DetectMissionTreesCommand request, CancellationToken token)
    {
        if (!await CanAccessAsync(request.TenantId, request.FarmId, token))
            return Denied();

        if (!double.IsFinite(request.ConfidenceThreshold) || request.ConfidenceThreshold is < 0 or > 1 ||
            !double.IsFinite(request.RowSpacing) || request.RowSpacing <= 0 ||
            !double.IsFinite(request.ColSpacing) || request.ColSpacing <= 0 ||
            request.PolygonGeoJson is { ValueKind: not (JsonValueKind.Object or JsonValueKind.Null) })
            return Result.Failure<MissionYoloJobResponse>(
                AppError.Validation("Yolo.InvalidInput", "Provide valid confidence, spacing and GeoJSON."));

        await using var tx = await db.Database.BeginTransactionAsync(token);
        var locked = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM mission.drone_missions
            WHERE id = {request.MissionId} AND tenant_id = {request.TenantId} AND farm_id = {request.FarmId}
            FOR UPDATE
            """).ToListAsync(token);
        if (locked.Count == 0)
            return Result.Failure<MissionYoloJobResponse>(MissionError.NotFound(request.MissionId));

        var tiling = (await jobs.FindAsync(request.TenantId, request.FarmId, request.MissionId,
            MissionImageJobStore.Tiling, request.TilingJobId, token))?.ToResponse();
        if (tiling is null && request.TilingJobId.HasValue)
            return Result.Failure<MissionYoloJobResponse>(
                AppError.NotFound("Tiling.JobNotFound", "The selected tiling job was not found."));

        if (tiling is not { Status: "COMPLETED", Result.ManifestStorageKey: { } manifest, Result.TotalTiles: > 0 })
            return Result.Failure<MissionYoloJobResponse>(
                AppError.Conflict("Tiling.JobNotCompleted", "Select a tiling job that has completed successfully."));

        var input = JsonSerializer.Serialize(new MissionYoloInput(
            tiling.JobId, manifest, tiling.Result.TotalTiles, request.ConfidenceThreshold, request.RowSpacing,
            request.ColSpacing, request.AnchorLat, request.AnchorLon, request.CustomAngleDeg,
            request.PolygonGeoJson ?? tiling.PolygonGeoJson));
        var active = await jobs.FindActiveAsync(request.MissionId, MissionImageJobStore.Yolo, token);
        if (active is not null)
        {
            if (!await jobs.MatchesAsync(active.Id, input, token))
                return Result.Failure<MissionYoloJobResponse>(
                    AppError.Conflict("Yolo.JobAlreadyActive", "Wait for the current YOLO job before changing its inputs."));

            await tx.CommitAsync(token);
            return Result.Success(active.ToYoloResponse());
        }

        var id = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        await jobs.InsertAsync(id, request.TenantId, request.FarmId, request.MissionId, MissionImageJobStore.Yolo,
            input, now, token);
        await tx.CommitAsync(token);
        return Result.Success(new MissionYoloJobResponse(
            id, request.MissionId, tiling.JobId, "QUEUED", now, null, null, null, null));
    }

    public async Task<Result<MissionYoloJobResponse>> Handle(GetMissionYoloJobQuery request, CancellationToken token)
    {
        if (!await CanAccessAsync(request.TenantId, request.FarmId, token))
            return Denied();

        var job = await jobs.FindAsync(request.TenantId, request.FarmId, request.MissionId,
            MissionImageJobStore.Yolo, request.JobId, token);
        return job is null
            ? Result.Failure<MissionYoloJobResponse>(AppError.NotFound("Yolo.JobNotFound", "YOLO job was not found."))
            : Result.Success(job.ToYoloResponse());
    }

    private async Task<bool> CanAccessAsync(Guid tenantId, Guid farmId, CancellationToken token)
    {
        var access = await managerAccess.ResolveFarmAccessAsync(farmId, token);
        return access.IsAllowed && access.TenantId == tenantId && access.FarmId == farmId;
    }

    private static Result<MissionYoloJobResponse> Denied() =>
        Result.Failure<MissionYoloJobResponse>(AppError.Forbidden(
            "Yolo.FarmAccessDenied", "The manager is not assigned to this farm."));
}
