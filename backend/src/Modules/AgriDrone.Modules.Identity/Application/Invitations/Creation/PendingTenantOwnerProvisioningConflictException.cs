namespace AgriDrone.Modules.Identity.Application.Invitations.Creation;

internal sealed class PendingTenantOwnerProvisioningConflictException(
    Exception innerException)
    : Exception(
        "A pending owner provisioning request already exists for this tenant.",
        innerException);
