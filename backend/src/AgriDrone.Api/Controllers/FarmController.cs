using AgriDrone.Api.Contracts.Farms;
using AgriDrone.Api.Contracts.Tenants;
using AgriDrone.Modules.Farms.Application.Abstractions.Features.GetFarm;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgriDrone.SharedInfrastructure.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace AgriDrone.Api.Controllers
{
    [Route("api/farms")]
    [ApiController]
    public class FarmController(
        ISender sender) : ControllerBase
    {
        [HttpGet]
        [Authorize(Policy = )]
        public async Task<IResult> GetFarms(
            [FromQuery] GetFarmsRequest request,
            CancellationToken cancellationToken)
        {
            var command = new GetFarmQuery(
                request.TenantId,
                request.PageNumber,
                request.PageSize);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                users => Results.Ok(users));
        }
    }
}
