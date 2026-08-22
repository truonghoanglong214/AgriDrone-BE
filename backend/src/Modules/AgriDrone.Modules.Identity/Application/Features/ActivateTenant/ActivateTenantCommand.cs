using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.ActivateTenant
{
    public sealed record ActivateTenantCommand(
    Guid TenantId)
    : IRequest<Result>;
}
