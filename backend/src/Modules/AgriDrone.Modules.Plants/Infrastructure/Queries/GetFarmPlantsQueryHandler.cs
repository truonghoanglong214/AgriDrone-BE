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

        var orderPaymentPending = await db.Database.SqlQueryRaw<bool>(
            @"SELECT EXISTS (
                SELECT 1
                FROM survey.survey_orders o
                WHERE o.farm_id = {0}
                  AND UPPER(o.status::text) <> 'CANCELLED'
                  AND (
                    (o.requires_baseline_mapping AND UPPER(o.deposit_payment_status::text) <> 'PAID')
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
                        )
                        AND UPPER(o.status::text) <> 'COMPLETED'
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
}
