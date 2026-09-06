using AgriDrone.Api.Contracts.TenantMembership;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.Modules.Identity.Application.Features.UpdateTenantMembershipStatus;
using AgriDrone.Modules.Identity.Application.Features.UpdateTenantRole;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[Route("api/tenants/current/members")]
[ApiController]
[Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
public sealed class TenantMembershipController(ISender sender) : ControllerBase
{
    /// <summary>Cập nhật vai trò thành viên trong tenant hiện tại.</summary>
    /// <remarks>
    /// Tenant Owner chuyển vai trò giữa Member và Tenant Admin. Endpoint không
    /// cho phép gán hoặc gỡ vai trò Owner và không cho actor tự đổi vai trò.
    /// </remarks>
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

    /// <summary>Cập nhật trạng thái thành viên trong tenant hiện tại.</summary>
    /// <remarks>
    /// Tenant Owner kích hoạt hoặc vô hiệu hóa membership của một thành viên.
    /// Membership Owner đang hoạt động không thể bị vô hiệu hóa qua endpoint này.
    /// </remarks>
    [HttpPut("{userId:guid}/status")]
    public async Task<IResult> UpdateStatus(
        [FromRoute] Guid userId,
        [FromBody] UpdateTenantMembershipStatusRequest request,
        CancellationToken cancellationToken)
    {
        var status = request.Status switch
        {
            UpdateTenantMembershipStatusValue.Active => GeneralStatus.Active,
            UpdateTenantMembershipStatusValue.Inactive => GeneralStatus.Inactive,
            _ => (GeneralStatus)(-1)
        };

        var result = await sender.Send(
            new UpdateTenantMembershipStatusCommand(userId, status),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            () => Results.NoContent());
    }
}
