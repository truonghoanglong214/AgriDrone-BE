using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.ActivateFarm
{
    internal sealed record ActivateFarmCommand(
        Guid FarmId,
        long ExpectedVersion
    ) : IRequest<Result>;
}
