using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.CreateFarm
{
    public sealed record CreateFarmResponse(
        Guid farmId,
        Guid tenantId,
        string code,
        string name,
        string? address,
        Polygon? boundary,
        Point? centerPoint,
        decimal? areaHectares,
        GeneralStatus status,
        Guid createdBy,
        DateTimeOffset createdAt);
}
