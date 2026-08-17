using AgriDrone.Api.Contracts.TenantInvitations;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Identity.Application.Features.AcceptTenantInvitation;
using AgriDrone.Modules.Identity.Application.Features.InviteTenantAdmin;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[Route("api/auth")]
[ApiController]
public sealed class TenantInvitationController(ISender sender) : ControllerBase
{
    [HttpPost("/current/invitations/tenant-admin")]
    [Authorize(Policy = IdentityAuthorizationPolicies.TenantOwner)]
    public async Task<IResult> InviteTenantAdmin(
        [FromBody] InviteTenantAdminRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new InviteTenantAdminCommand(request.Email),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                response,
                statusCode: StatusCodes.Status201Created));
    }

    [AllowAnonymous]
    [HttpPost("/invitations/accept")]
    public async Task<IResult> AcceptTenantInvitation(
        [FromBody] AcceptTenantInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AcceptTenantInvitationCommand(
                request.Token,
                request.Password,
                request.FullName,
                request.Phone),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(response));
    }
}
