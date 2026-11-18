using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMaps;

public sealed record GetFarmBaseMapsQuery(
    Guid FarmId,
    bool IncludeDraft = false)
    : IRequest<Result<IReadOnlyList<FarmBaseMapSummaryResponse>>>;

