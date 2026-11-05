using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.PlantHealth;

/// <summary>
/// UC-28: Ky thuat vien xem anh crop, bam Confirm hoac Reject tung vet benh.
/// Khi Confirmed: doi review_status = CONFIRMED, cap nhat plant.plants health level sang MODERATE.
/// Khi Rejected: doi review_status = REJECTED, neu cay khong con vet CONFIRMED nao thi hoan lai HEALTHY.
/// </summary>
internal sealed class ReviewPlantObservationCommandHandler(
    MissionsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    ILogger<ReviewPlantObservationCommandHandler> logger)
    : IRequestHandler<ReviewPlantObservationCommand, Result<ReviewPlantObservationResult>>
{
    private sealed record ObservationRecord(
        Guid Id,
        Guid MissionId,
        Guid FarmId,
        Guid? SuggestedPlantId,
        Guid? ResolvedPlantId,
        string ReviewStatus,
        string? MatchingParametersJson);

    public async Task<Result<ReviewPlantObservationResult>> Handle(
        ReviewPlantObservationCommand request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<ReviewPlantObservationResult>(
                AppError.Forbidden("ReviewObservation.FarmAccessDenied", "Access denied."));
        }

        // Normalize decision
        var decision = request.Decision.Trim().ToUpperInvariant();
        if (decision is not ("CONFIRMED" or "REJECTED"))
        {
            return Result.Failure<ReviewPlantObservationResult>(
                AppError.Validation("ReviewObservation.InvalidDecision",
                    "Decision must be 'CONFIRMED' or 'REJECTED'."));
        }

        // Lay observation
        var obs = await dbContext.Database.SqlQueryRaw<ObservationRecord>(@"
            SELECT id AS ""Id"", mission_id AS ""MissionId"", farm_id AS ""FarmId"",
                   suggested_plant_id AS ""SuggestedPlantId"", resolved_plant_id AS ""ResolvedPlantId"",
                   review_status::text AS ""ReviewStatus"",
                   matching_parameters::text AS ""MatchingParametersJson""
            FROM mission.mission_plant_observations
            WHERE id = {0} AND farm_id = {1}",
            request.ObservationId, request.FarmId)
            .FirstOrDefaultAsync(cancellationToken);

        if (obs is null)
        {
            return Result.Failure<ReviewPlantObservationResult>(
                AppError.NotFound("ReviewObservation.NotFound",
                    $"Observation {request.ObservationId} not found."));
        }

        var dbStatus = decision == "CONFIRMED" ? "CONFIRMED" : "REJECTED";

        if (string.Equals(obs.ReviewStatus, dbStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(new ReviewPlantObservationResult(
                request.ObservationId,
                dbStatus,
                obs.ResolvedPlantId ?? obs.SuggestedPlantId));
        }

        var now = DateTimeOffset.UtcNow;

        // 1. Cap nhat trang thai observation (enum PostgreSQL: CONFIRMED, REJECTED)
        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE mission.mission_plant_observations
            SET review_status = {dbStatus}::system.observation_review_status,
                reviewed_at = {now}
            WHERE id = {request.ObservationId}",
            cancellationToken);

        // 2. Cap nhat condition_detections neu co class benh duoc sua
        if (!string.IsNullOrWhiteSpace(request.CorrectedConditionCode)
            && decision == "CONFIRMED")
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE plant.condition_detections cd
                SET condition_id = (
                    SELECT id FROM plant.plant_conditions
                    WHERE code = {request.CorrectedConditionCode.Trim().ToUpperInvariant()}
                    AND is_active = true LIMIT 1
                )
                FROM plant.plant_scans ps
                WHERE cd.plant_scan_id = ps.id
                  AND ps.mission_id = {obs.MissionId}
                  AND ps.plant_id = {obs.SuggestedPlantId}
                  AND cd.condition_id IS NOT NULL",
                cancellationToken);
        }

        // 3. Cap nhat health level cho cay
        var plantId = obs.ResolvedPlantId ?? obs.SuggestedPlantId;
        if (plantId.HasValue)
        {
            var isDisease = true;
            if (!string.IsNullOrWhiteSpace(obs.MatchingParametersJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(obs.MatchingParametersJson);
                    if (doc.RootElement.TryGetProperty("class_name", out var clsProp))
                    {
                        var clsName = clsProp.GetString();
                        if (clsName != null && clsName.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase))
                        {
                            isDisease = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse matching parameters for obs {ObsId}", request.ObservationId);
                }
            }

            if (decision == "CONFIRMED")
            {
                if (isDisease)
                {
                    // Cay duoc xac nhan benh -> cap nhat sang MODERATE
                    await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE plant.plants
                        SET current_health_level_id = (
                            SELECT id FROM plant.health_levels WHERE code = 'MODERATE' LIMIT 1
                        ), last_inspected_at = {now}, updated_at = {now}
                        WHERE id = {plantId.Value} AND farm_id = {request.FarmId}",
                        cancellationToken);
                }
                else
                {
                    // Cay xac nhan trai / khoe -> giu HEALTHY
                    await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE plant.plants
                        SET current_health_level_id = (
                            SELECT id FROM plant.health_levels WHERE code = 'HEALTHY' LIMIT 1
                        ), last_inspected_at = {now}, updated_at = {now}
                        WHERE id = {plantId.Value} AND farm_id = {request.FarmId}",
                        cancellationToken);
                }
            }
            else // REJECTED
            {
                // Kiem tra cay con vet benh nao (CONFIRMED hoac MATCHED) khac khong
                var activeDiseaseCount = await dbContext.Database.SqlQueryRaw<int>(@"
                    SELECT COUNT(*)::int AS ""Value""
                    FROM mission.mission_plant_observations
                    WHERE farm_id = {0}
                      AND (suggested_plant_id = {1} OR resolved_plant_id = {1})
                      AND review_status <> 'REJECTED'::system.observation_review_status
                      AND id <> {2}
                      AND (matching_parameters->>'class_name') NOT ILIKE 'Healthy%'",
                    request.FarmId, plantId.Value, request.ObservationId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (activeDiseaseCount == 0)
                {
                    // Khong con vet benh nao -> hoan ve HEALTHY
                    await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE plant.plants
                        SET current_health_level_id = (
                            SELECT id FROM plant.health_levels WHERE code = 'HEALTHY' LIMIT 1
                        ), last_inspected_at = {now}, updated_at = {now}
                        WHERE id = {plantId.Value} AND farm_id = {request.FarmId}",
                        cancellationToken);
                }
            }
        }

        logger.LogInformation(
            "Observation {ObsId} reviewed: {Decision} by manager, plant={PlantId}",
            request.ObservationId, decision, plantId);

        return Result.Success(new ReviewPlantObservationResult(
            request.ObservationId,
            dbStatus,
            plantId));
    }
}

