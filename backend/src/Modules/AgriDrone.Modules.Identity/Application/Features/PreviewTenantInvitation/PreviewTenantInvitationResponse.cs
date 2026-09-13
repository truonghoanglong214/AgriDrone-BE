using AgriDrone.Modules.Identity.Domain.Tenants;

namespace AgriDrone.Modules.Identity.Application.Features.PreviewTenantInvitation;

public sealed record PreviewTenantInvitationResponse(
    string MaskedEmail,
    string TenantName,
    TenantMemberRole Role,
    DateTimeOffset ExpiresAt,
    bool RequiresAccountCreation);
