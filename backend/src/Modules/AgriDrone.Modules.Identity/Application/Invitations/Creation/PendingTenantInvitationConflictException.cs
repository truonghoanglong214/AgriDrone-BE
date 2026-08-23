namespace AgriDrone.Modules.Identity.Application.Invitations.Creation;

internal sealed class PendingTenantInvitationConflictException(
    Exception innerException)
    : Exception(
        "A pending invitation already exists for this tenant and email.",
        innerException);
