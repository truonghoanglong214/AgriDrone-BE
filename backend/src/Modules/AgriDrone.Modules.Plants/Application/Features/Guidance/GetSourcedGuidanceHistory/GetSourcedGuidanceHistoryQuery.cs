using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.GetSourcedGuidanceHistory;

public sealed record GetSourcedGuidanceHistoryQuery
    : IRequest<Result<IReadOnlyList<SourcedGuidanceResponse>>>;
