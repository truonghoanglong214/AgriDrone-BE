using AgriDrone.Api.Contracts.Farms;
using AgriDrone.Api.Contracts.Tenants;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Farms.Application.Features.GetFarm;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using AgriDrone.Modules.Farms.Application.Features.CreateFarm;
using AgriDrone.Modules.Identity.Application.Features.RegisterUser;
using AgriDrone.Modules.Farms.Application.Features.GetFarmById;
using AgriDrone.Modules.Farms.Application.Features.UpdateFarmDetail;

namespace AgriDrone.Api.Controllers
{
    [Route("api/farms")]
    [ApiController]
    public class FarmController(
        ISender sender) : ControllerBase
    {
        [HttpGet]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantAdmin)]
        public async Task<IResult> GetFarms(
            [FromQuery] GetFarmsRequest request,
            CancellationToken cancellationToken)
        {
            var command = new GetFarmQuery(
                request.PageNumber,
                request.PageSize);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farms => Results.Ok(FarmResponseMapper.ToResponse(farms)));
        }

        [HttpPost]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantAdmin)]
        public async Task<IResult> CreateFarm(
            [FromBody] CreateFarmRequest request,
            CancellationToken cancellationToken)
        {
            var command =  new CreateFarmCommand(
                request.Code,
                request.Name,
                request.Address,
                GeoJsonGeometryMapper.ToPolygon(request.Boundary),
                GeoJsonGeometryMapper.ToPoint(request.CenterPoint),
                request.AreaHectares);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farm => Results.Ok(FarmResponseMapper.ToResponse(farm)));
        }

        [HttpGet("{farmId:guid}")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantMember)]
        public async Task<IResult> GetFarmById(
            [FromRoute] GetFarmByIdRequest request,
            CancellationToken cancellationToken)
        {
            var command = new GetFarmByIdCommand(request.FarmId);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farm => Results.Ok(FarmResponseMapper.ToResponse(farm)));
        }

        [HttpPut("{farmId:guid}")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantAdmin)]
        public async Task<IResult> UpdateFarmDetail(
            [FromRoute] Guid farmId,
            [FromBody] UpdateFarmDetailRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateFarmDetailCommand(
                farmId,
                request.Name,
                request.Address,
                GeoJsonGeometryMapper.ToPolygon(request.Boundary),
                GeoJsonGeometryMapper.ToPoint(request.CenterPoint),
                request.AreaHectares,
                request.ExpectedVersion);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                farm => Results.Ok(FarmResponseMapper.ToResponse(farm)));
        }
    }
}
