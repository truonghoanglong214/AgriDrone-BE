using AgriDrone.SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.CreateTenant
{
    public sealed record CreateTenantResponse(
    Guid TenantId,
    string Code,
    string Name,
    GeneralStatus Status,
    DateTimeOffset CreatedAt);
}
