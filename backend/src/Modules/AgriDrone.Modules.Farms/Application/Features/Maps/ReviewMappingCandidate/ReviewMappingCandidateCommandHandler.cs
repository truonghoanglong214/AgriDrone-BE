using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ReviewMappingCandidate;

internal sealed class ReviewMappingCandidateCommandHandler(
    FarmsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ReviewMappingCandidateCommand, Result<CandidateReviewResponse>>
{
    public async Task<Result<CandidateReviewResponse>> Handle(
        ReviewMappingCandidateCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<CandidateReviewResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<CandidateReviewResponse>(FarmError.NotFound());
        }

        var baseMap = await dbContext.FarmBaseMapVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                m => m.Id == command.DraftId && m.FarmId == command.FarmId,
                cancellationToken);

        var batch = await dbContext.MappingCandidateBatches
            .SingleOrDefaultAsync(
                b => b.FarmBaseMapVersionId == command.DraftId && b.FarmId == command.FarmId,
                cancellationToken);

        if (batch is null)
        {
            return Result.Failure<CandidateReviewResponse>(
                AppError.NotFound("MappingCandidateBatch.NotFound", "Mapping candidate batch not found."));
        }

        var candidate = await dbContext.MappingCandidates
            .SingleOrDefaultAsync(
                c => c.BatchId == batch.Id && c.CandidateId == command.CandidateId,
                cancellationToken);

        var now = timeProvider.GetUtcNow();

        if (candidate is null && command.Decision == MappingCandidateReviewDecision.CreateNew)
        {
            var newCandidatePos = command.CorrectedPosition ?? throw new InvalidOperationException("CreateNew requires coordinates.");
            var newCandId = command.CandidateId != Guid.Empty ? command.CandidateId : Guid.NewGuid();
            var newRow = command.CorrectedRowIndex ?? 1;
            var newCol = command.CorrectedColumnIndex ?? 1;
            candidate = MappingCandidate.Create(
                id: Guid.NewGuid(),
                batchId: batch.Id,
                zoneId: null,
                candidateId: newCandId,
                sourceObservationId: Guid.NewGuid(),
                evidenceMediaAssetIds: [],
                originalPosition: newCandidatePos,
                proposedRowIndex: newRow,
                proposedColumnIndex: newCol,
                positionConfidence: 1.0m,
                boundaryClassification: "Inside",
                measuredBoundaryDistanceM: 0m,
                boundaryThresholdM: 0m,
                proposedMatchedPlantId: null,
                matchConfidence: 1.0m,
                createdAt: now,
                treeId: $"manual_{newCandId:N}"[..16]);
            dbContext.MappingCandidates.Add(candidate);
        }
        else if (candidate is null)
        {
            return Result.Failure<CandidateReviewResponse>(
                AppError.NotFound("MappingCandidate.NotFound", $"Mapping candidate {command.CandidateId} not found in this batch."));
        }

        if (batch.Status == MappingCandidateBatchStatus.PendingReview)
        {
            batch.StartReview(actorId, now);
        }

        var isPublished = baseMap is not null && baseMap.Status is FarmBaseMapStatus.Published or FarmBaseMapStatus.Superseded;

        // Nếu là bản đồ đã published và hành động Relocate/Reject yêu cầu matched plant ID:
        Guid? matchedPlantId = command.CorrectedMatchedPlantId ?? candidate.CorrectedMatchedPlantId ?? candidate.ProposedMatchedPlantId;
        if (!matchedPlantId.HasValue && isPublished)
        {
            matchedPlantId = await FindTargetPlantIdAsync(command.FarmId, command.DraftId, candidate, cancellationToken);
        }

        try
        {
            candidate.ApplyReview(
                decision: command.Decision,
                correctedPosition: command.CorrectedPosition,
                correctedRowIndex: command.CorrectedRowIndex,
                correctedColumnIndex: command.CorrectedColumnIndex,
                correctedMatchedPlantId: matchedPlantId,
                reviewerId: actorId,
                reason: command.Reason,
                reviewedAt: now);

            await dbContext.SaveChangesAsync(cancellationToken);

            // Nếu là bản đồ PUBLISHED: cập nhật trực tiếp vào plant.plants và lưu biến động vào plant.plant_change_events
            if (isPublished)
            {
                await ApplyPublishedTreeChangesAsync(
                    command,
                    candidate,
                    actorId,
                    matchedPlantId,
                    now,
                    cancellationToken);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<CandidateReviewResponse>(
                AppError.Validation("MappingCandidate.ReviewFailed", ex.Message));
        }

        var response = new CandidateReviewResponse(
            CandidateId: candidate.CandidateId,
            ReviewDecision: candidate.ReviewDecision.ToString(),
            CorrectedLongitude: candidate.CorrectedPosition?.X,
            CorrectedLatitude: candidate.CorrectedPosition?.Y,
            CorrectedRowIndex: candidate.CorrectedRowIndex,
            CorrectedColumnIndex: candidate.CorrectedColumnIndex,
            CorrectedMatchedPlantId: candidate.CorrectedMatchedPlantId,
            Reason: candidate.ReviewReason,
            ReviewedAt: candidate.ReviewedAt ?? now);

        return Result.Success(response);
    }

    private async Task<Guid?> FindTargetPlantIdAsync(
        Guid farmId,
        Guid farmBaseMapVersionId,
        MappingCandidate candidate,
        CancellationToken cancellationToken)
    {
        var row = candidate.ProposedRowIndex;
        var col = candidate.ProposedColumnIndex;

        if (row.HasValue && col.HasValue)
        {
            var plantId = await dbContext.Database
                .SqlQueryRaw<Guid>(
                    """
                    SELECT id AS "Value"
                    FROM plant.plants
                    WHERE farm_id = {0}
                      AND farm_base_map_version_id = {1}
                      AND row_index = {2}
                      AND column_index = {3}
                      AND lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status
                    LIMIT 1
                    """,
                    farmId, farmBaseMapVersionId, row.Value, col.Value)
                .FirstOrDefaultAsync(cancellationToken);

            if (plantId != Guid.Empty)
                return plantId;
        }

        var pos = candidate.OriginalPosition;
        if (pos is not null)
        {
            var plantId = await dbContext.Database
                .SqlQueryRaw<Guid>(
                    """
                    SELECT id AS "Value"
                    FROM plant.plants
                    WHERE farm_id = {0}
                      AND farm_base_map_version_id = {1}
                      AND lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status
                    ORDER BY location <-> ST_SetSRID(ST_MakePoint({2}, {3}), 4326)
                    LIMIT 1
                    """,
                    farmId, farmBaseMapVersionId, pos.X, pos.Y)
                .FirstOrDefaultAsync(cancellationToken);

            if (plantId != Guid.Empty)
                return plantId;
        }

        return null;
    }

    private async Task ApplyPublishedTreeChangesAsync(
        ReviewMappingCandidateCommand command,
        MappingCandidate candidate,
        Guid actorId,
        Guid? matchedPlantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var farmId = command.FarmId;
        var versionId = command.DraftId;
        var reason = command.Reason;

        switch (command.Decision)
        {
            case MappingCandidateReviewDecision.Reject:
            {
                var row = candidate.ProposedRowIndex ?? command.CorrectedRowIndex;
                var col = candidate.ProposedColumnIndex ?? command.CorrectedColumnIndex;

                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    WITH target_plant AS (
                        SELECT id, location, row_index, column_index, lifecycle_status
                        FROM plant.plants
                        WHERE farm_id = {farmId}
                          AND farm_base_map_version_id = {versionId}
                          AND (id = {matchedPlantId} OR (row_index = {row} AND column_index = {col}))
                        LIMIT 1
                    ),
                    updated AS (
                        UPDATE plant.plants p
                        SET lifecycle_status = 'REMOVED'::system.plant_lifecycle_status,
                            retired_at = {now},
                            updated_at = {now}
                        FROM target_plant t
                        WHERE p.id = t.id
                        RETURNING p.id, t.location, t.row_index, t.column_index, t.lifecycle_status
                    )
                    INSERT INTO plant.plant_change_events (
                        id, farm_id, farm_base_map_version_id, mission_id, plant_id,
                        change_type, source, old_location, new_location,
                        old_row_index, new_row_index, old_column_index, new_column_index,
                        old_lifecycle_status, new_lifecycle_status,
                        created_by, status, notes, reviewed_by, reviewed_at, created_at
                    )
                    SELECT
                        gen_random_uuid(), {farmId}, {versionId}, NULL, u.id,
                        'REMOVED_PLANT'::system.plant_change_type, 'MANUAL'::system.plant_change_source,
                        u.location, u.location,
                        u.row_index, u.row_index, u.column_index, u.column_index,
                        u.lifecycle_status, 'REMOVED'::system.plant_lifecycle_status,
                        {actorId}, 'CONFIRMED'::system.review_status, {reason ?? "Removed manually on published base map"},
                        {actorId}, {now}, {now}
                    FROM updated u
                    """, cancellationToken);
                break;
            }

            case MappingCandidateReviewDecision.Relocate:
            {
                var newPos = command.CorrectedPosition ?? candidate.OriginalPosition;
                var oldRow = candidate.ProposedRowIndex;
                var oldCol = candidate.ProposedColumnIndex;
                var newRow = command.CorrectedRowIndex ?? oldRow ?? 1;
                var newCol = command.CorrectedColumnIndex ?? oldCol ?? 1;

                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    WITH target_plant AS (
                        SELECT id, location, row_index, column_index, lifecycle_status
                        FROM plant.plants
                        WHERE farm_id = {farmId}
                          AND farm_base_map_version_id = {versionId}
                          AND (id = {matchedPlantId} OR (row_index = {oldRow} AND column_index = {oldCol}))
                        LIMIT 1
                    ),
                    updated AS (
                        UPDATE plant.plants p
                        SET location = ST_SetSRID(ST_MakePoint({newPos.X}, {newPos.Y}), 4326),
                            row_index = {newRow},
                            column_index = {newCol},
                            position_source = 'MANUAL'::system.position_source,
                            updated_at = {now}
                        FROM target_plant t
                        WHERE p.id = t.id
                        RETURNING p.id, t.location AS old_location, t.row_index AS old_row, t.column_index AS old_col, t.lifecycle_status
                    )
                    INSERT INTO plant.plant_change_events (
                        id, farm_id, farm_base_map_version_id, mission_id, plant_id,
                        change_type, source, old_location, new_location,
                        old_row_index, new_row_index, old_column_index, new_column_index,
                        old_lifecycle_status, new_lifecycle_status,
                        created_by, status, notes, reviewed_by, reviewed_at, created_at
                    )
                    SELECT
                        gen_random_uuid(), {farmId}, {versionId}, NULL, u.id,
                        'MAPPING_DIFFERENCE'::system.plant_change_type, 'MANUAL'::system.plant_change_source,
                        u.old_location, ST_SetSRID(ST_MakePoint({newPos.X}, {newPos.Y}), 4326),
                        u.old_row, {newRow}, u.old_col, {newCol},
                        u.lifecycle_status, u.lifecycle_status,
                        {actorId}, 'CONFIRMED'::system.review_status, {reason ?? "Relocated manually on published base map"},
                        {actorId}, {now}, {now}
                    FROM updated u
                    """, cancellationToken);
                break;
            }

            case MappingCandidateReviewDecision.CreateNew:
            {
                var newPos = command.CorrectedPosition ?? candidate.OriginalPosition;
                var newRow = command.CorrectedRowIndex ?? candidate.ProposedRowIndex ?? 1;
                var newCol = command.CorrectedColumnIndex ?? candidate.ProposedColumnIndex ?? 1;
                var plantCode = $"P-{farmId.ToString()[..4].ToUpperInvariant()}-{newRow:D3}-{newCol:D3}-{Guid.NewGuid().ToString()[..4].ToUpperInvariant()}";
                var unknownHealthId = Guid.Parse("11111111-1111-4111-8111-111111111101");

                await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                    WITH inserted_plant AS (
                        INSERT INTO plant.plants (
                            id, farm_id, zone_id, plant_code, location, lifecycle_status,
                            row_index, column_index, location_accuracy_m, position_confidence,
                            position_source, farm_base_map_version_id, current_health_level_id,
                            created_at, updated_at, mapped_at
                        ) VALUES (
                            gen_random_uuid(), {farmId}, NULL, {plantCode},
                            ST_SetSRID(ST_MakePoint({newPos.X}, {newPos.Y}), 4326),
                            'ACTIVE'::system.plant_lifecycle_status,
                            {newRow}, {newCol}, 0.5, 1.0,
                            'MANUAL'::system.position_source,
                            {versionId}, {unknownHealthId},
                            {now}, {now}, {now}
                        )
                        RETURNING id, location, row_index, column_index
                    )
                    INSERT INTO plant.plant_change_events (
                        id, farm_id, farm_base_map_version_id, mission_id, plant_id,
                        change_type, source, old_location, new_location,
                        old_row_index, new_row_index, old_column_index, new_column_index,
                        old_lifecycle_status, new_lifecycle_status,
                        created_by, status, notes, reviewed_by, reviewed_at, created_at
                    )
                    SELECT
                        gen_random_uuid(), {farmId}, {versionId}, NULL, p.id,
                        'NEW_PLANT'::system.plant_change_type, 'MANUAL'::system.plant_change_source,
                        NULL, p.location,
                        NULL, p.row_index, NULL, p.column_index,
                        NULL, 'ACTIVE'::system.plant_lifecycle_status,
                        {actorId}, 'CONFIRMED'::system.review_status, {reason ?? "Created manually on published base map"},
                        {actorId}, {now}, {now}
                    FROM inserted_plant p
                    """, cancellationToken);
                break;
            }
        }
    }
}
