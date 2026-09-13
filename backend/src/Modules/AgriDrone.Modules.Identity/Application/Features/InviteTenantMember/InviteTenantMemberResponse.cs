namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantMember;

public sealed record InviteTenantMemberResponse(
    Guid InvitationId,
    string Email,
    DateTimeOffset ExpiresAt);
