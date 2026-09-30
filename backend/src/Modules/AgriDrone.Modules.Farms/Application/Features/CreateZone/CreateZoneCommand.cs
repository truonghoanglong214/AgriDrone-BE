using System.Runtime.InteropServices.Marshalling;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Identity;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Features.CreateZone
{
    public sealed record CreateZoneCommand(
        Guid FarmId,
        string Code,
        string Name,
        Polygon? Boundary,
        decimal? AreaHectares) : IRequest<Result<CreateZoneResponse>>;
}

