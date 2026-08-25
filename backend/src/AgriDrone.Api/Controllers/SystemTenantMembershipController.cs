using AgriDrone.Modules.Identity.Application.Features.ActivateTenantMembership;
using AgriDrone.Modules.Identity.Application.Features.DeactivateTenantMembership;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers
{
    [Route("api/system/tenant-membership")]
    [ApiController]
    public class SystemTenantMembershipController(
        ISender sender) : ControllerBase
    {
        [HttpPut("{tenantId:guid}/activate")]
        public async Task<IResult> Activate(
            [FromRoute] Guid tenantId,
            CancellationToken cancellationToken)
        {
            var command = new ActivateTenantMembershipCommand(
                tenantId);

            var result = await sender.Send(
                command, 
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        }

        [HttpPut("{tenantId:guid}/deactivate")]
        public async Task<IResult> Deactivate(
            [FromRoute] Guid tenantId,
            CancellationToken cancellation)
        {
            var command = new DeactivateTenantMembershipCommand(
                tenantId);

            var result = await sender.Send(
                command,
                cancellation);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        } 
    }
}
