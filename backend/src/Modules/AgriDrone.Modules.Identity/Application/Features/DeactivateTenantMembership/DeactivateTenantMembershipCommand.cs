using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.DeactivateTenantMembership
{
    public sealed record DeactivateTenantMembershipCommand(
        Guid tenantId) : IRequest<Result>;
}
