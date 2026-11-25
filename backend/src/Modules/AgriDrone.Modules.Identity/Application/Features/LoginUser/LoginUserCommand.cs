using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.LoginUser;

public sealed record LoginUserCommand(
    string email,
    string password,
    bool issueRefreshSession = false) : IRequest<Result<AuthenticationCommandResult>>;
