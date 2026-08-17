using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.ResetPassword;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword,
    string ConfirmPassword) : IRequest<Result<ResetPasswordResponse>>;
