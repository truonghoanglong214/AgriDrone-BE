using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Application.Features.GetHealthLevels
{
    public sealed record HealthLevelResponse(
        Guid Id,
        string Code,
        string Name,
        int? Rank,
        bool IsHealthy,
        string? Description);
}
