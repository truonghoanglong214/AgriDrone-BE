using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;

namespace AgriDrone.Modules.Plants.Application.Abstractions.Queries;

internal interface IPlantHealthMasterDataQueries
{
    Task<PlantHealthMasterDataResponse> GetActiveAsync(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SourcedGuidanceResponse>> GetGuidanceHistoryAsync(
        CancellationToken cancellationToken = default);
}
