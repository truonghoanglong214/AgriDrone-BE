using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenant
{
    public sealed record TenantListItemResponse(
        Guid Id,
        string Code,
        string Name,
        GeneralStatus Status,
        DateTimeOffset CreatedAt);
}
