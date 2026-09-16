using AgriDrone.Modules.Plants.Application.Features.GetHealthLevels;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Application.Abstractions.Queries
{
    internal interface IHealthLevelQueries
    {
        Task<IReadOnlyList<HealthLevelResponse>> GetActiveAsync(CancellationToken cancellationToken = default);
    }
}
