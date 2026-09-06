using AgriDrone.Api.Contracts.Users;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.Modules.Identity.Application.Features.GetTenantUsers;
using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.Modules.Identity.Application.Features.UpdateUser;
using AgriDrone.Modules.Identity.Application.Features.UpdateUserPassword;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UserController(ISender sender) : ControllerBase
    {
        /// <summary>Lấy danh sách người dùng toàn hệ thống.</summary>
        /// <remarks>
        /// Trả danh sách phân trang dành cho System Admin, không giới hạn theo
        /// tenant đang chọn.
        /// </remarks>
        [HttpGet]
        [Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
        public async Task<IResult> GetUsers(
        [FromQuery] GetUserRequest request,
        CancellationToken cancellationToken)
        {
            var query = new GetUsersQuery(
                request.PageNumber,
                request.PageSize);

            var result = await sender.Send(
                query,
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                users => Results.Ok(users));
        }

        /// <summary>Lấy thành viên của tenant hiện tại.</summary>
        /// <remarks>
        /// Trả danh sách người dùng và tenant membership theo tenant trong access
        /// token. Chỉ Tenant Admin hoặc Owner được phép truy vấn.
        /// </remarks>
        [HttpGet("/tenants/current/users")]
        [Authorize(Policy = AccessAuthorizationPolicies.TenantAdmin)]
        public async Task<IResult> GetTenantUsers(
            [FromQuery] GetTenantUserRequest request,
            CancellationToken cancellationToken)
        {
            var query = new GetTenantUsersQuery(
                request.PageNumber,
                request.PageSize);

            var result = await sender.Send(query, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                users => Results.Ok(users));
        }

        /// <summary>Cập nhật hồ sơ người dùng hiện tại.</summary>
        /// <remarks>
        /// Cập nhật họ tên và số điện thoại của người dùng đang đăng nhập; email
        /// và quyền truy cập không thay đổi qua endpoint này.
        /// </remarks>
        [HttpPut("/current/profile")]
        [Authorize]
        public async Task<IResult> UpdateUserProfile(
            [FromBody] UpdateUserProfileRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateUserCommand(
                request.Name,
                request.Phone);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                users => Results.Ok(users));
        }

        /// <summary>Đổi mật khẩu của người dùng hiện tại.</summary>
        /// <remarks>
        /// Xác minh mật khẩu cũ trước khi lưu mật khẩu mới cho tài khoản đang
        /// đăng nhập.
        /// </remarks>
        [HttpPut("/current/change-password")]
        [Authorize]
        public async Task<IResult> UpdatePassword(
            [FromBody] UpdateUserPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateUserPasswordCommand(
                request.NewPassword,
                request.OldPassword);
            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                users => Results.Ok(users));
        }
    }
}
