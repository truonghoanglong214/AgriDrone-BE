using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.ActivateSourcedGuidance;

public sealed record ActivateSourcedGuidanceCommand(
    Guid GuidanceId,
    uint ExpectedVersion)
    : IRequest<Result<SourcedGuidanceResponse>>;
