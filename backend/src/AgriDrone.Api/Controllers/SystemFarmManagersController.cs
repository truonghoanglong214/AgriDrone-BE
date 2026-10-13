using AgriDrone.Api.Contracts.SystemManagers;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system/farms/{farmId:guid}/primary-manager")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemFarmManagersController(ISender sender) : ControllerBase
{
    [HttpPut]
    public async Task<IResult> AssignOrReassign(
        [FromRoute] Guid farmId,
        [FromBody] AssignPrimaryFarmManagerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssignPrimaryFarmManagerCommand(
                farmId,
                request.SystemManagerProfileId,
                request.Reason,
                request.ExpectedCurrentAssignmentVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPut("end")]
    public async Task<IResult> End(
        [FromRoute] Guid farmId,
        [FromBody] EndPrimaryFarmManagerAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new EndPrimaryFarmManagerAssignmentCommand(
                farmId,
                request.Reason,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, () => Results.NoContent());
    }
}
