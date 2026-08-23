using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class TenantInvitationRepository(IdentityDbContext context)
    : ITenantInvitationRepository
{
    public void Add(TenantInvitation invitation) =>
        context.TenantInvitations.Add(invitation);

    public Task<TenantInvitation?> GetByIdAsync(Guid invitationId, CancellationToken cancellationToken)
        => context.TenantInvitations.SingleOrDefaultAsync(
            invitation => invitation.Id == invitationId,
            cancellationToken);

    public Task<TenantInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        context.TenantInvitations.SingleOrDefaultAsync(
            invitation => invitation.TokenHash == tokenHash,
            cancellationToken);

    public Task<TenantInvitation?> GetPendingAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken) =>
        context.TenantInvitations.SingleOrDefaultAsync(
            invitation =>
                invitation.TenantId == tenantId &&
                invitation.Email == email &&
                invitation.Status == TenantInvitationStatus.Pending,
            cancellationToken);

    public Task<TenantInvitation?> GetPendingOwnerProvisioningAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        context.TenantInvitations.SingleOrDefaultAsync(
            invitation =>
                invitation.TenantId == tenantId &&
                invitation.Purpose == TenantInvitationPurpose.OwnerProvisioning &&
                invitation.Status == TenantInvitationStatus.Pending,
            cancellationToken);
}
