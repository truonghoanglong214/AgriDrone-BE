using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.GetSourcedGuidanceHistory;

internal sealed class GetSourcedGuidanceHistoryQueryHandler(
    IPlantHealthMasterDataQueries queries)
    : IRequestHandler<GetSourcedGuidanceHistoryQuery,
        Result<IReadOnlyList<SourcedGuidanceResponse>>>
{
    public async Task<Result<IReadOnlyList<SourcedGuidanceResponse>>> Handle(
        GetSourcedGuidanceHistoryQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await queries.GetGuidanceHistoryAsync(cancellationToken));
}
