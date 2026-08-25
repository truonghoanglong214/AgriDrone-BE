using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.Tenants
{
    public interface ITenantMembershipRepository
    {
        void Add(TenantMembership tenantMembership);
        Task<IReadOnlyCollection<TenantMembership>> GetActiveByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken);

        Task<TenantMembership?> GetActiveByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken);

        Task<bool> HasActiveOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken);

        Task<TenantMembership?> GetByUserAndTenantIdAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);

        Task<TenantMembership?> GetByIdAsync(
            Guid membershipId,
            CancellationToken cancellationToken);
    }
}
