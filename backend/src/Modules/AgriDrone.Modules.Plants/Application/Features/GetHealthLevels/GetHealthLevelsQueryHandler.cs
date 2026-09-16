using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Application.Features.GetHealthLevels
{
    internal sealed class GetHealthLevelsQueryHandler(
        IHealthLevelQueries healthLevelQueries
        ) : IRequestHandler<GetHealthLevelsQuery, Result<IReadOnlyList<HealthLevelResponse>>>
    {
        public async Task<Result<IReadOnlyList<HealthLevelResponse>>> Handle(GetHealthLevelsQuery request, CancellationToken cancellationToken)
        {
            var healthLevels = await healthLevelQueries.GetActiveAsync(cancellationToken);
            return Result.Success(healthLevels);
        }
    }
}
