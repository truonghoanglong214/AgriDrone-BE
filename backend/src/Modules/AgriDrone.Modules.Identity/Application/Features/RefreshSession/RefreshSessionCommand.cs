using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.RefreshSession;

public sealed record RefreshSessionCommand(
    string? RawRefreshToken) : IRequest<Result<AuthenticationCommandResult>>;
