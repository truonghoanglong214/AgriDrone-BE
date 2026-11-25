using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.LogoutSession;

public sealed record LogoutSessionCommand(
    string? RawRefreshToken) : IRequest<Result>;
