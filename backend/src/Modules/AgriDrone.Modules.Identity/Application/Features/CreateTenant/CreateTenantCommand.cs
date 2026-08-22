using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.CreateTenant
{
    public sealed record CreateTenantCommand(
    string Code,
    string Name)
    : IRequest<Result<CreateTenantResponse>>;
}
