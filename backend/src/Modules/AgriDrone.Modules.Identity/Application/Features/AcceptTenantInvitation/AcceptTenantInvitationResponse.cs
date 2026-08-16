using AgriDrone.Modules.Identity.Domain.Tenants;

namespace AgriDrone.Modules.Identity.Application.Features.AcceptTenantInvitation;

public sealed record AcceptTenantInvitationResponse(
    Guid UserId,
    Guid TenantId,
    TenantMemberRole Role,
    bool AccountCreated);
