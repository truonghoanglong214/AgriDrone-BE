using AgriDrone.Api.Contracts.TenantInvitations;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.Modules.Identity.Application.Features.AcceptTenantInvitation;
using AgriDrone.Modules.Identity.Application.Features.PreviewTenantInvitation;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[Route("api/auth")]
[ApiController]
public sealed class TenantInvitationController(ISender sender) : ControllerBase
{
    /// <summary>Xem trước lời mời và xác định có cần tạo tài khoản.</summary>
    /// <remarks>
    /// Token phải còn hiệu lực và thuộc tenant đang hoạt động. Endpoint chỉ trả
    /// email đã che một phần để frontend chọn đúng biểu mẫu accept invitation.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("invitations/preview")]
    public async Task<IResult> PreviewTenantInvitation(
        [FromBody] PreviewTenantInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PreviewTenantInvitationQuery(request.Token),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(response));
    }

    /// <summary>Chấp nhận lời mời tham gia tenant.</summary>
    /// <remarks>
    /// Xác thực invitation token, tạo tài khoản nếu cần và kích hoạt tenant
    /// membership theo vai trò đã được mời. Token chỉ được sử dụng một lần.
    /// </remarks>
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
