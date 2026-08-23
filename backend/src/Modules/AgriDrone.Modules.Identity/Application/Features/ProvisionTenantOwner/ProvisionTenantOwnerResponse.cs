namespace AgriDrone.Modules.Identity.Application.Features.ProvisionTenantOwner;

public sealed record ProvisionTenantOwnerResponse(
    Guid InvitationId,
    string Email,
    DateTimeOffset ExpiresAt);
