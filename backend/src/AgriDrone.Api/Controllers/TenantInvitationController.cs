using AgriDrone.Api.Contracts.TenantInvitations;
using AgriDrone.SharedInfrastructure.Authorization;
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
    /// <summary>Mời Tenant Admin vào tenant hiện tại.</summary>
    /// <remarks>
    /// Tenant Owner tạo lời mời có thời hạn cho email được chỉ định. Lời mời dùng
    /// để tạo hoặc liên kết tài khoản với membership Tenant Admin.
    /// </remarks>
    [HttpPost("/current/invitations/tenant-admin")]
    [Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
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
