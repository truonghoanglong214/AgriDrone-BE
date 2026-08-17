using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.ForgotPassword;

public sealed record ForgotPasswordCommand(
    string Email) : IRequest<Result<ForgotPasswordResponse>>;
