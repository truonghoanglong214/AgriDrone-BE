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
