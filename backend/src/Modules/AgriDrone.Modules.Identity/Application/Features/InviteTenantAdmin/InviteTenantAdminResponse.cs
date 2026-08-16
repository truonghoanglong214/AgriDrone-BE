namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantAdmin;

public sealed record InviteTenantAdminResponse(
    Guid InvitationId,
    string Email,
    DateTimeOffset ExpiresAt);
