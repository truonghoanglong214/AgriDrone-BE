using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record UpdateSystemManagerAvailabilityCommand(
    Guid ProfileId,
    SystemManagerAvailabilityStatus Availability,
    string Reason,
    long ExpectedVersion) : IRequest<Result<SystemManagerProfileResponse>>;
