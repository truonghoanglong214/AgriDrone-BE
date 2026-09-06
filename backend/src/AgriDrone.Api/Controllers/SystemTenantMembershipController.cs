using AgriDrone.SharedInfrastructure.Authorization;
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
    [Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
    public sealed class SystemTenantMembershipController(
        ISender sender) : ControllerBase
    {
        /// <summary>Kích hoạt tenant membership.</summary>
        /// <remarks>
        /// System Admin khôi phục quyền truy cập tenant cho membership được chỉ
        /// định và ghi nhận thay đổi trạng thái phục vụ audit.
        /// </remarks>
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

        /// <summary>Vô hiệu hóa tenant membership.</summary>
        /// <remarks>
        /// System Admin thu hồi quyền truy cập tenant của membership được chỉ
        /// định. Membership Owner đang hoạt động được bảo vệ khỏi thao tác này.
        /// </remarks>
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
