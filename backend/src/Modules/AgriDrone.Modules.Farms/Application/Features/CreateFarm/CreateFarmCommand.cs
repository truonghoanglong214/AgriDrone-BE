using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.CreateFarm
{
    public sealed record CreateFarmCommand(
        string code,
        string name,
        string? address,
        Polygon? boundary,
        Point? centerPoint,
        decimal? areaHectares) : IRequest<Result<CreateFarmResponse>>;
}
