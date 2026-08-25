using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AgriDrone.SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories
{
    internal sealed class TenantMembershipRepository(IdentityDbContext context) : ITenantMembershipRepository
    {  
        public void Add(TenantMembership tenantMembership) =>context.TenantMemberships.Add(tenantMembership);      
        public async Task<IReadOnlyCollection<TenantMembership>> GetActiveByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return await context.TenantMemberships
                .AsNoTracking()
                .Include(membership => membership.Tenant)
                .Where(membership =>
                    membership.UserId == userId &&
                    membership.Status == GeneralStatus.Active &&
                    membership.Tenant.Status == GeneralStatus.Active &&
                    membership.Tenant.DeletedAt == null)
                .OrderBy(membership => membership.Tenant.Name)
                .ToArrayAsync(cancellationToken);
        }

        public Task<TenantMembership?> GetActiveByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            return context.TenantMemberships
                .AsNoTracking()
                .Include(membership => membership.Tenant)
                .SingleOrDefaultAsync(
                    membership =>
                        membership.UserId == userId &&
                        membership.TenantId == tenantId &&
                        membership.Status == GeneralStatus.Active &&
                        membership.Tenant.Status == GeneralStatus.Active &&
                        membership.Tenant.DeletedAt == null,
                    cancellationToken);
        }

        public Task<bool> HasActiveOwnerAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            context.TenantMemberships.AnyAsync(
                membership =>
                    membership.TenantId == tenantId &&
                    membership.Role == TenantMemberRole.Owner &&
                    membership.Status == GeneralStatus.Active,
                cancellationToken);

        public Task<TenantMembership?> GetByUserAndTenantIdAsync(
            Guid userId,
            Guid tenantId,
            CancellationToken cancellationToken) =>
            context.TenantMemberships
                .Include(membership => membership.User)
                .SingleOrDefaultAsync(
                    membership =>
                        membership.TenantId == tenantId &&
                        membership.UserId == userId,
                    cancellationToken);

        public Task<TenantMembership?> GetByIdAsync(
            Guid membershipId,
            CancellationToken cancellationToken) =>
            context.TenantMemberships.SingleOrDefaultAsync(
                membership => membership.Id == membershipId,
                cancellationToken);
    }
}
