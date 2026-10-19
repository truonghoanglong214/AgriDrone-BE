using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.RetireSourcedGuidance;

public sealed record RetireSourcedGuidanceCommand(
    Guid GuidanceId,
    uint ExpectedVersion)
    : IRequest<Result>;
