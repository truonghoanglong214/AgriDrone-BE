using AgriDrone.Api.Contracts.Tenants;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.Modules.Identity.Application.Features.ActivateTenant;
using AgriDrone.Modules.Identity.Application.Features.CreateTenant;
using AgriDrone.Modules.Identity.Application.Features.DeactivateTenant;
using AgriDrone.Modules.Identity.Application.Features.GetTenant;
using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.Modules.Identity.Application.Features.ProvisionTenantOwner;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers
{
    [Route("api/system/tenants")]
    [ApiController]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
    public sealed class SystemTenantsController(ISender sender) : ControllerBase
    {
        /// <summary>Tạo tenant mới.</summary>
        /// <remarks>
        /// System Admin tạo bản ghi tenant; việc cấp Owner được thực hiện riêng
        /// qua owner provisioning.
        /// </remarks>
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

        /// <summary>Kích hoạt tenant.</summary>
        /// <remarks>
        /// Chuyển tenant sang trạng thái Active để các membership hợp lệ có thể
        /// sử dụng tenant làm ngữ cảnh nghiệp vụ.
        /// </remarks>
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

        /// <summary>Vô hiệu hóa tenant.</summary>
        /// <remarks>
        /// Chuyển tenant sang trạng thái Inactive; các request nghiệp vụ mới của
        /// tenant sẽ bị từ chối bởi kiểm tra effective access.
        /// </remarks>
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

        /// <summary>Cấp Owner cho tenant.</summary>
        /// <remarks>
        /// System Admin tạo lời mời Owner Provisioning cho email được chỉ định.
        /// Membership Owner chỉ được tạo khi người nhận chấp nhận lời mời và tenant
        /// chưa có Owner đang hoạt động.
        /// </remarks>
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

        /// <summary>Lấy danh sách tenant toàn hệ thống.</summary>
        /// <remarks>Trả danh sách tenant phân trang dành riêng cho System Admin.</remarks>
        [HttpGet("all")]
        public async Task<IResult> GetAllTenantAsync(
            [FromQuery] GetTenantRequest request,
            CancellationToken cancellationToken)
        {
            var query = new GetTenantsQuery(
                request.PageNumber,
                request.PageSize);

            var result = await sender.Send(
                query,
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                tenants => Results.Ok(tenants));
        }
    }
}
