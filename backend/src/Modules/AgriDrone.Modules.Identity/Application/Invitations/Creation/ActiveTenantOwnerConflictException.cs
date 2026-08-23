namespace AgriDrone.Modules.Identity.Application.Invitations.Creation;

internal sealed class ActiveTenantOwnerConflictException(
    Exception innerException)
    : Exception(
        "An active owner already exists for this tenant.",
        innerException);
