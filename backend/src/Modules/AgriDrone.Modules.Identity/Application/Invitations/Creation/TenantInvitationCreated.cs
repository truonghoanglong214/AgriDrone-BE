namespace AgriDrone.Modules.Identity.Application.Invitations.Creation;

internal sealed record TenantInvitationCreated(
    Guid InvitationId,
    string Email,
    DateTimeOffset ExpiresAt);
