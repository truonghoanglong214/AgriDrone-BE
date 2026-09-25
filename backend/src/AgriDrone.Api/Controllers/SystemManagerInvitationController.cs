using AgriDrone.Api.Contracts.SystemManagerInvitations;
using AgriDrone.Modules.Identity.Application.Features.AcceptSystemManagerInvitation;
using AgriDrone.Modules.Identity.Application.Features.PreviewSystemManagerInvitation;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/auth/system-manager-invitations")]
public sealed class SystemManagerInvitationController(
    ISender sender)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("preview")]
    public async Task<IResult> Preview(
        [FromBody] PreviewSystemManagerInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PreviewSystemManagerInvitationQuery(
                request.Token),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(response));
    }

    [AllowAnonymous]
    [HttpPost("accept")]
    public async Task<IResult> Accept(
        [FromBody] AcceptSystemManagerInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AcceptSystemManagerInvitationCommand(
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