using System.Data;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Plants.Application.Features.GetFarmPlants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Queries;

internal sealed class GetFarmPlantsQueryHandler(
    PlantsDbContext db,
    ISurveyRequestFarmReferenceQuery farmReferenceQuery,
    IExecutionContext executionContext)
    : IRequestHandler<GetFarmPlantsQuery, Result<IReadOnlyList<FarmPlantItem>>>
{
    public async Task<Result<IReadOnlyList<FarmPlantItem>>> Handle(
        GetFarmPlantsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.FarmId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<FarmPlantItem>>(
                AppError.Validation("Plant.InvalidFarmId", "Farm ID is required."));
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<FarmPlantItem>>(
                AppError.Unauthorized("Plant.TenantRequired", "Tenant is required."));
        }

        var farm = await farmReferenceQuery.GetAsync(request.FarmId, cancellationToken);
        if (farm is null || farm.TenantId != tenantId)
        {
            return Result.Failure<IReadOnlyList<FarmPlantItem>>(
                AppError.NotFound("Plant.FarmNotFound", "Farm was not found."));
        }

        // Read the order scope and inventory from one database snapshot so an
        // internal approval cannot change the live rows between those reads.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var usesV3Inventory = await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM survey.survey_orders o
                WHERE o.tenant_id = {tenantId} AND o.farm_id = {request.FarmId}
                  AND o.scope = 'V3'::system.contract_scope
                  AND (
                      o.status <> 'CANCELLED'::system.survey_order_status
                      OR EXISTS (
                          SELECT 1
                          FROM farm.farm_base_map_versions m
                          WHERE m.source_survey_order_id = o.id
                            AND m.tenant_id = o.tenant_id AND m.farm_id = o.farm_id
                            AND m.approved_internal_at IS NOT NULL
                      )
                  )
            ) AS "Value"
            """).SingleAsync(cancellationToken);
        if (usesV3Inventory)
            return await ReadReleasedV3PlantsAsync(tenantId, request.FarmId, cancellationToken);

        var orderPaymentPending = await db.Database.SqlQueryRaw<bool>(
            @"SELECT EXISTS (
                SELECT 1
                FROM survey.survey_orders o
                WHERE o.farm_id = {0}
                  AND o.id = (
                    SELECT m.source_survey_order_id FROM farm.farm_base_map_versions m
                    WHERE m.farm_id = o.farm_id AND m.status::text = 'PUBLISHED'
                    ORDER BY m.version_number DESC LIMIT 1
                  )
                  AND UPPER(o.status::text) <> 'CANCELLED'
                  AND (
                    (o.deposit_amount > 0 AND UPPER(o.deposit_payment_status::text) <> 'PAID')
                    OR UPPER(o.status::text) IN (
                        'PENDING_BOUNDARY_VERIFICATION',
                        'AWAITING_BASELINE_APPOINTMENT',
                        'AWAITING_BASELINE_DEPOSIT',
                        'AWAITING_BOUNDARY_CAPTURE',
                        'BASELINE_READY',
                        'BASELINE_IN_PROGRESS',
                        'AWAITING_BASELINE_REVIEW',
                        'AWAITING_PRICING',
                        'AWAITING_PAID_APPOINTMENT',
                        'AWAITING_PAYMENT'
                    )
                    OR (
                        o.final_price IS NOT NULL
                        AND COALESCE(o.remaining_amount, o.final_price) > 0
                        AND NOT EXISTS (
                            SELECT 1
                            FROM survey.survey_payments p
                            WHERE p.survey_order_id = o.id
                              AND (p.purpose IN ('FinalBalance', 'FINAL_BALANCE') OR UPPER(REPLACE(p.purpose::text, '_', '')) = 'FINALBALANCE')
                              AND UPPER(p.status::text) = 'PAID'
                              AND p.amount = COALESCE(o.remaining_amount, o.final_price)
                              AND p.currency = o.currency
                        )
                    )
                  )
              ) AS ""Value""",
            request.FarmId)
            .FirstOrDefaultAsync(cancellationToken);

        if (orderPaymentPending)
        {
            return Result.Failure<IReadOnlyList<FarmPlantItem>>(
                AppError.Conflict(
                    "FarmPlants.PaymentRequired",
                    "The survey results for this farm are locked. Please complete the remaining payment to unlock results."));
        }

        var rows = await db.Plants
            .AsNoTracking()
            .Where(plant => plant.FarmId == request.FarmId)
            .OrderBy(plant => plant.RowIndex)
            .ThenBy(plant => plant.ColumnIndex)
            .ThenBy(plant => plant.PlantCode)
            .Select(plant => new
            {
                plant.Id,
                plant.PlantCode,
                plant.ZoneId,
                plant.RowIndex,
                plant.ColumnIndex,
                Lat = plant.Location == null ? (double?)null : plant.Location.Y,
                Lon = plant.Location == null ? (double?)null : plant.Location.X,
                plant.LifecycleStatus
            })
            .ToArrayAsync(cancellationToken);

        IReadOnlyList<FarmPlantItem> plants = rows
            .Select(plant => new FarmPlantItem(
                plant.Id,
                plant.PlantCode,
                plant.ZoneId,
                plant.RowIndex,
                plant.ColumnIndex,
                plant.Lat,
                plant.Lon,
                plant.LifecycleStatus.ToString().ToUpperInvariant()))
            .ToArray();

        return Result.Success(plants);
    }

    private async Task<Result<IReadOnlyList<FarmPlantItem>>> ReadReleasedV3PlantsAsync(
        Guid tenantId, Guid farmId, CancellationToken cancellationToken)
    {
        var snapshotId = await db.Database.SqlQuery<Guid?>($"""
            SELECT a.pole_count_snapshot_id AS "Value"
            FROM survey.v3_survey_releases r
            JOIN survey.survey_internal_approved_result_revisions a ON a.id = r.approval_id
                AND a.survey_order_id = r.survey_order_id AND a.tenant_id = r.tenant_id AND a.farm_id = r.farm_id
            JOIN farm.farm_base_map_versions m ON m.id = r.map_id
                AND m.source_survey_order_id = r.survey_order_id AND m.tenant_id = r.tenant_id AND m.farm_id = r.farm_id
            WHERE r.tenant_id = {tenantId} AND r.farm_id = {farmId}
              AND NOT EXISTS (
                  SELECT 1
                  FROM survey.quality_refund_cases c
                  WHERE c.v3_publication_id = r.id AND c.status = 'Refunded'
              )
            ORDER BY m.version_number DESC, r.published_at DESC, r.id DESC
            LIMIT 1
            """).FirstOrDefaultAsync(cancellationToken);
        if (!snapshotId.HasValue)
            return Result.Failure<IReadOnlyList<FarmPlantItem>>(
                AppError.NotFound("Plant.NotFound", "No released plant inventory was found."));

        // The approved snapshot fixes the plant membership and positions.
        // Live zone assignments and lifecycle changes may belong to a later survey.
        var plants = await db.Database.SqlQuery<FarmPlantItem>($"""
            SELECT released."Id", released."PlantCode", NULL::uuid AS "ZoneId",
                   released."Row", released."Col", released."Lat", released."Lon",
                   'ACTIVE'::text AS "LifecycleStatus"
            FROM (
                SELECT DISTINCT ON (i.plant_profile_id)
                       p.id AS "Id", p.plant_code AS "PlantCode",
                       i.effective_row_index AS "Row", i.effective_column_index AS "Col",
                       ST_Y(i.effective_position) AS "Lat", ST_X(i.effective_position) AS "Lon"
                FROM survey.survey_order_pole_count_snapshot_items i
                JOIN survey.survey_order_pole_count_snapshots s ON s.id = i.snapshot_id
                    AND s.tenant_id = i.tenant_id AND s.farm_id = i.farm_id
                JOIN plant.plants p ON p.plant_profile_id = i.plant_profile_id AND p.farm_id = i.farm_id
                    AND p.created_at <= s.verified_at
                WHERE i.snapshot_id = {snapshotId.Value} AND i.tenant_id = {tenantId} AND i.farm_id = {farmId}
                  AND i.is_accepted AND i.is_strictly_in_boundary
                ORDER BY i.plant_profile_id, p.created_at DESC, p.id, i.id
            ) released
            ORDER BY released."Row", released."Col", released."PlantCode"
            """).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<FarmPlantItem>>(plants);
    }
}
