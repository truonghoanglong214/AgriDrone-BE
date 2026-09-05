using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.CreateZone
{
    public sealed record CreateZoneResponse(
        Guid ZoneId,
        Guid FarmId,
        string Code,
        string Name,
        Polygon? Boundary,
        decimal? AreaHectares,
        GeneralStatus Status,
        long Version,
        DateTimeOffset CreatedAt,
        Guid CreatedBy
    );
}
