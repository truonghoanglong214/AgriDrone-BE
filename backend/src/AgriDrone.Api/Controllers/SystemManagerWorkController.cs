using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerWorkController(ISender sender) : ControllerBase
{
    [HttpGet("farms")]
    public async Task<IResult> GetMyAssignedFarms(
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetMyAssignedFarmsQuery(),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
