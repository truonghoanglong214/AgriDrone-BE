using AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;

namespace AgriDrone.Modules.Plants.Application.Abstractions.Queries;

internal interface IPlantConditionQueries
{
    Task<IReadOnlyList<PlantConditionCatalogResponse>> GetActiveAsync(
        CancellationToken cancellationToken = default);
}
