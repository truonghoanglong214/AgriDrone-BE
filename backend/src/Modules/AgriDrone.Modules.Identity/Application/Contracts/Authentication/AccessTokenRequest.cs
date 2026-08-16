using AgriDrone.Modules.Identity.Domain.Tenants;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Contracts.Authentication
{
    public sealed record AccessTokenRequest(
        Guid UserId,
        string Email,
        Guid? TenantId,
        Guid? TenantMembershipId,
        TenantMemberRole? TenantRole,
        IReadOnlyCollection<string> SystemRoles);
}
