namespace AgriDrone.Modules.Identity.Domain.TenantInvitations;

public interface ITenantInvitationRepository
{
    Task<TenantInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task<TenantInvitation?> GetPendingAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken);

    Task<TenantInvitation?> GetPendingOwnerProvisioningAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<TenantInvitation?> GetByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    void Add(TenantInvitation invitation);
}
