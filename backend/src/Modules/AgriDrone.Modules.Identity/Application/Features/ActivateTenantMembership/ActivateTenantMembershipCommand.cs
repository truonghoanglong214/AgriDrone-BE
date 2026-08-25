using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.ActivateTenantMembership
{
    public sealed record ActivateTenantMembershipCommand(
        Guid tenantId) : IRequest<Result>;
}
