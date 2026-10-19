using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;

internal sealed class GetPlantHealthMasterDataQueryHandler(
    IPlantHealthMasterDataQueries queries,
    TimeProvider timeProvider)
    : IRequestHandler<GetPlantHealthMasterDataQuery,
        Result<PlantHealthMasterDataResponse>>
{
    public async Task<Result<PlantHealthMasterDataResponse>> Handle(
        GetPlantHealthMasterDataQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await queries.GetActiveAsync(
            timeProvider.GetUtcNow(),
            cancellationToken));
}
