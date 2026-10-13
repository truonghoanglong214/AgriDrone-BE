using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record CreateSystemManagerProfileCommand(Guid UserId)
    : IRequest<Result<SystemManagerProfileResponse>>;
