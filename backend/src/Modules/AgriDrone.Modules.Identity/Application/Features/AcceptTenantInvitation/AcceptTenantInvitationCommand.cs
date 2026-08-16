using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.AcceptTenantInvitation;

public sealed record AcceptTenantInvitationCommand(
    string Token,
    string? Password,
    string? FullName,
    string? Phone) : IRequest<Result<AcceptTenantInvitationResponse>>;
