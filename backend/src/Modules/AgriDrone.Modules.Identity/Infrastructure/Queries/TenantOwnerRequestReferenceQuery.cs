using AgriDrone.IntegrationContracts.Identity;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Queries;

internal sealed class TenantOwnerRequestReferenceQuery(
    IdentityDbContext context)
    : ITenantOwnerRequestReferenceQuery
{
    public Task<TenantOwnerRequestReference?> GetActiveOwnerAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);

        return context.TenantMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Role == TenantMemberRole.Owner &&
                membership.Status == GeneralStatus.Active &&
                membership.Tenant.Status == GeneralStatus.Active &&
                membership.Tenant.DeletedAt == null &&
                membership.User.Status == UserStatus.Active &&
                membership.User.DeletedAt == null)
            .Select(membership => new TenantOwnerRequestReference(
                membership.TenantId,
                membership.UserId,
                membership.User.FullName,
                membership.User.Email,
                membership.User.Phone))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
