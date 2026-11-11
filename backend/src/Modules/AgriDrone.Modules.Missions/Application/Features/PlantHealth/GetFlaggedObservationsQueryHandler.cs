using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Application.Features.PlantHealth;

/// <summary>
/// UC-29 (GET): Lay danh sach observation bi co PENDING (cay lech toa do > 2.5m,
/// chua gan duoc vao cay nao) de Manager xu ly trong tab "Flagged Locations".
/// </summary>
internal sealed class GetFlaggedObservationsQueryHandler(
    MissionsDbContext dbContext,
    ISystemManagerAccessService managerAccessService)
    : IRequestHandler<GetFlaggedObservationsQuery, Result<GetFlaggedObservationsResult>>
{
    private sealed record FlaggedRow(
        Guid ObservationId,
        Guid MissionId,
        double? DetectedLat,
        double? DetectedLon,
        double? GpsDistanceMeters,
        double? DetectionConfidence,
        string? MatchingParametersJson,
        Guid? SuggestedPlantId);

    public async Task<Result<GetFlaggedObservationsResult>> Handle(
        GetFlaggedObservationsQuery request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<GetFlaggedObservationsResult>(
                AppError.Forbidden("GetFlaggedObs.FarmAccessDenied", "Access denied."));
        }

        var rows = await dbContext.Database.SqlQueryRaw<FlaggedRow>(@"
            SELECT
                id                                              AS ""ObservationId"",
                mission_id                                      AS ""MissionId"",
                ST_Y(detected_location::geometry)               AS ""DetectedLat"",
                ST_X(detected_location::geometry)               AS ""DetectedLon"",
                gps_distance_m                                  AS ""GpsDistanceMeters"",
                detection_confidence                            AS ""DetectionConfidence"",
                matching_parameters::text                       AS ""MatchingParametersJson"",
                suggested_plant_id                              AS ""SuggestedPlantId""
            FROM mission.mission_plant_observations
            WHERE farm_id = {0}
              AND review_status = 'PENDING'::system.observation_review_status
            ORDER BY created_at DESC",
            request.FarmId)
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new FlaggedObservationItem(
            r.ObservationId,
            r.MissionId,
            r.DetectedLat,
            r.DetectedLon,
            r.GpsDistanceMeters,
            r.DetectionConfidence,
            r.MatchingParametersJson,
            r.SuggestedPlantId))
            .ToList();

        return Result.Success(new GetFlaggedObservationsResult(items));
    }
}

