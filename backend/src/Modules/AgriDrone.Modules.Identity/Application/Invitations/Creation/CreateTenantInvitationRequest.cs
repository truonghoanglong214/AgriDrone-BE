using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;

namespace AgriDrone.Modules.Identity.Application.Invitations.Creation;

internal sealed record CreateTenantInvitationRequest(
    Guid TenantId,
    Guid InvitedByUserId,
    string Email,
    TenantMemberRole Role,
    TenantInvitationPurpose Purpose);
