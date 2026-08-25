using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Identity.Application.Features.ActivateTenantMembership;
using AgriDrone.Modules.Identity.Application.Features.DeactivateTenantMembership;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers
{
    [Route("api/system/tenant-memberships")]
    [ApiController]
    [Authorize(Policy = IdentityAuthorizationPolicies.SystemAdmin)]
    public sealed class SystemTenantMembershipController(
        ISender sender) : ControllerBase
    {
        [HttpPut("{tenantMembershipId:guid}/activate")]
        public async Task<IResult> Activate(
            [FromRoute] Guid tenantMembershipId,
            CancellationToken cancellationToken)
        {
            var command = new ActivateTenantMembershipCommand(
                tenantMembershipId);

            var result = await sender.Send(
                command, 
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        }

        [HttpPut("{tenantMembershipId:guid}/deactivate")]
        public async Task<IResult> Deactivate(
            [FromRoute] Guid tenantMembershipId,
            CancellationToken cancellation)
        {
            var command = new DeactivateTenantMembershipCommand(
                tenantMembershipId);

            var result = await sender.Send(
                command,
                cancellation);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        } 
    }
}
