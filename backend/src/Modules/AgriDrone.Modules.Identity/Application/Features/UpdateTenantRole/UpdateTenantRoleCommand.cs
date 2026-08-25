using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateTenantRole
{
    public sealed record UpdateTenantRoleCommand(
        Guid UserId,
        TenantMemberRole Role) : IRequest<Result>;
}
