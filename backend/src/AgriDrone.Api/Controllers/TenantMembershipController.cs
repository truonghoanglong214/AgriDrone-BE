using AgriDrone.Api.Contracts.TenantMembership;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Identity.Application.Features.UpdateTenantRole;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[Route("api/tenants/current/members")]
[ApiController]
[Authorize(Policy = IdentityAuthorizationPolicies.TenantOwner)]
public sealed class TenantMembershipController(ISender sender) : ControllerBase
{
    [HttpPut("{userId:guid}/role")]
    public async Task<IResult> UpdateRole(
        [FromRoute] Guid userId,
        [FromBody] UpdateTenantRoleRequest request,
        CancellationToken cancellationToken)
    {
        var role = request.Role switch
        {
            UpdateTenantRoleValue.Member => TenantMemberRole.Member,
            UpdateTenantRoleValue.TenantAdmin => TenantMemberRole.TenantAdmin,
            _ => (TenantMemberRole)(-1)
        };

        var result = await sender.Send(
            new UpdateTenantRoleCommand(userId, role),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            () => Results.NoContent());
    }
}
