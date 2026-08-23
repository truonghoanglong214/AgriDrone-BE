using AgriDrone.Api.Contracts.Tenants;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Identity.Application.Features.CreateTenant;
using MediatR;
using AgriDrone.SharedInfrastructure.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgriDrone.Modules.Identity.Application.Features.ActivateTenant;
using AgriDrone.Modules.Identity.Application.Features.DeactivateTenant;
using AgriDrone.Modules.Identity.Application.Features.ProvisionTenantOwner;

namespace AgriDrone.Api.Controllers
{
    [Route("api/system/tenants")]
    [ApiController]
    [Authorize(Policy = IdentityAuthorizationPolicies.SystemAdmin)]
    public sealed class SystemTenantsController(ISender sender) : ControllerBase
    {
        [HttpPost]
        public async Task<IResult> CreateTenantAsync(
            [FromBody] CreateTenantRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CreateTenantCommand(
                request.TenantCode,
                request.TenantName);

            var result = await sender.Send(
                command,
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                response => Results.Json(
                    response,
                    statusCode: StatusCodes.Status201Created));
        }

        [HttpPut("{tenantId:guid}/activate")]
        public async Task<IResult> ActivateTenant(
            [FromRoute] Guid tenantId,
            CancellationToken cancellationToken)
        {
            var command = new ActivateTenantCommand(
                tenantId);

            var result = await sender.Send(
                command, 
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        }

        [HttpPut("{tenantId:guid}/deactivate")]
        public async Task<IResult> DeactivateTenant(
            [FromRoute] Guid tenantId,
            CancellationToken cancellationToken)
        {
            var command = new DeactivateTenantCommand(
                tenantId);

            var result = await sender.Send(
                command,
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                () => Results.NoContent());
        }

        [HttpPost("{tenantId:guid}/owner-provisionings")]
        public async Task<IResult> ProvisionTenantOwner(
            [FromRoute] Guid tenantId,
            [FromBody] ProvisionTenantOwnerRequest request,
            CancellationToken cancellationToken)
        {
            var command = new ProvisionTenantOwnerCommand(
                tenantId,
                request.Email);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                response => Results.Json(
                    response,
                    statusCode: StatusCodes.Status201Created));
        }
    }
}
