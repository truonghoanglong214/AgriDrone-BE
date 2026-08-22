using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.DeactivateTenant
{
    public sealed record DeactivateTenantCommand(
    Guid TenantId)
    : IRequest<Result>;
}
