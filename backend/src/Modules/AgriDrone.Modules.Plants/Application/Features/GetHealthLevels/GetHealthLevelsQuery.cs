using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Application.Features.GetHealthLevels
{
    public sealed record GetHealthLevelsQuery : IRequest<Result<IReadOnlyList<HealthLevelResponse>>>;
}
