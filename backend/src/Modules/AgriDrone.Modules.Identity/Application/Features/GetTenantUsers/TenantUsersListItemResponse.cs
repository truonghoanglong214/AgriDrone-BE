using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenantUsers
{
    public sealed record TenantUsersListItemResponse(Guid Id,
    string Email,
    string FullName,
    string? Phone,
    UserStatus Status,
    TenantMemberRole Role,
    DateTimeOffset? JoinedAt);

}
