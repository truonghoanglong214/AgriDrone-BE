using AgriDrone.SharedKernel.Application;
using MediatR;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.UpdateFarmDetail;

public sealed record UpdateFarmDetailCommand(
    Guid farmId,
    string name,
    string? address,
    Polygon? boundary,
    Point? centerPoint,
    decimal? areaHectares,
    long expectedVersion) : IRequest<Result<UpdateFarmDetailResponse>>;
