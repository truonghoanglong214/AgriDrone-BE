using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record SuspendSystemManagerProfileCommand(
    Guid ProfileId,
    string Reason,
    long ExpectedVersion) : IRequest<Result<SystemManagerProfileResponse>>;
