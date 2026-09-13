using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.RestoreFarm
{
    public sealed record RestoreFarmCommand(
        Guid FarmId,
        long ExpectedVersion
    ) : IRequest<Result>;
}
