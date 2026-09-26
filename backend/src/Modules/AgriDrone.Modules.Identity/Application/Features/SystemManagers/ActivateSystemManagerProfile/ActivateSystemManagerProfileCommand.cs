using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record ActivateSystemManagerProfileCommand(
    Guid ProfileId,
    string Reason,
    long ExpectedVersion) : IRequest<Result<SystemManagerProfileResponse>>;
