using AgriDrone.Modules.Plants.Application.Features.GetHealthLevels;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers
{
    [Route("api/catalog")]
    [ApiController]
    [Authorize]
    public class PlantCatalogController(
        ISender sender) : ControllerBase
    {
        [HttpGet("health-levels")]
        public async Task<IResult> GetHealthLevels(CancellationToken cancellationToken)
        {
            var command = new GetHealthLevelsQuery();
            var result = await sender.Send(
                command, 
                cancellationToken);

            return result.ToHttpResult(
            HttpContext,
            levels => Results.Ok(levels));
        }
    }
}
