using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Features.GetFarm
{
    public sealed record FarmListItemResponse(
        Guid id,
        Guid tenantId,
        string code,
        string name,
        string? address,
        Polygon? boundary,
        Point? centerPoint,
        decimal? areaHectares,
        GeneralStatus status,
        DateTimeOffset createdAt,
        Guid createdBy);
}
