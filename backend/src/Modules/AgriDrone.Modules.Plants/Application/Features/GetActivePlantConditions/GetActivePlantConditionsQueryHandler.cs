using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;

internal sealed class GetActivePlantConditionsQueryHandler(
    IPlantConditionQueries plantConditionQueries)
    : IRequestHandler<
        GetActivePlantConditionsQuery,
        Result<IReadOnlyList<PlantConditionCatalogResponse>>>
{
    public async Task<Result<IReadOnlyList<PlantConditionCatalogResponse>>> Handle(
        GetActivePlantConditionsQuery request,
        CancellationToken cancellationToken)
    {
        var conditions = await plantConditionQueries.GetActiveAsync(cancellationToken);
        return Result.Success(conditions);
    }
}
