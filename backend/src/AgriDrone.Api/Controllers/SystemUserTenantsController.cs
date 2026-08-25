using AgriDrone.Api.Contracts.Tenants;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Identity.Application.Features.GetUserTenants;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[Route("api/system/users")]
[ApiController]
[Authorize(Policy = IdentityAuthorizationPolicies.SystemAdmin)]
public sealed class SystemUserTenantsController(ISender sender) : ControllerBase
{
    [HttpGet("{userId:guid}/tenants")]
    public async Task<IResult> GetUserTenantsAsync(
        [FromRoute] Guid userId,
        [FromQuery] GetUserTenantsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetUserTenantsQuery(
            userId,
            request.PageNumber,
            request.PageSize);

        var result = await sender.Send(
            query,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            tenants => Results.Ok(tenants));
    }
}
