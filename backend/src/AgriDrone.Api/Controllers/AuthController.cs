using AgriDrone.Api.Contracts.Users;
using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.Modules.Identity.Application.Features.ForgotPassword;
using AgriDrone.Modules.Identity.Application.Features.RegisterUser;
using AgriDrone.Modules.Identity.Application.Features.ResetPassword;
using AgriDrone.Modules.Identity.Application.Features.SelectTenant;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController(ISender sender) : ControllerBase
    {
        /// <summary>Đăng ký tài khoản và tenant mới.</summary>
        /// <remarks>
        /// Tạo đồng thời người dùng, tenant đang hoạt động và tenant membership
        /// với vai trò Owner cho người đăng ký.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
        {
            var command = new RegisterUserCommand(
                request.Email,
                request.Password,
                request.FullName,
                request.Phone,
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

        /// <summary>Đăng nhập hệ thống.</summary>
        /// <remarks>
        /// Xác thực email và mật khẩu. Nếu người dùng chỉ thuộc một tenant thì
        /// trả access token; nếu thuộc nhiều tenant thì trả selection token và
        /// danh sách tenant để người dùng chọn ngữ cảnh làm việc.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IResult> Login(
            [FromBody] LoginUserRequest request,
            CancellationToken cancellationToken)
        {
            var command = new LoginUserCommand(
                request.Email,
                request.Password);
            var result = await sender.Send(
                command,
                cancellationToken);
            return result.ToHttpResult(
                HttpContext,
                response => Results.Json(
                    response,
                    statusCode: StatusCodes.Status200OK));
        }

        /// <summary>Yêu cầu đặt lại mật khẩu.</summary>
        /// <remarks>
        /// Tạo token đặt lại mật khẩu và gửi liên kết qua email nếu tài khoản
        /// tồn tại. API luôn trả thông báo chung để không làm lộ email đăng ký.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IResult> ForgotPassword(
            [FromBody] ForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var command = new ForgotPasswordCommand(request.Email);
            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                response => Results.Json(
                    response,
                    statusCode: StatusCodes.Status202Accepted));
        }

        /// <summary>Đặt lại mật khẩu bằng token.</summary>
        /// <remarks>
        /// Tiêu thụ token đặt lại mật khẩu một lần và cập nhật mật khẩu mới
        /// cho tài khoản đang hoạt động.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IResult> ResetPassword(
            [FromBody] ResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var command = new ResetPasswordCommand(
                request.Token,
                request.NewPassword,
                request.ConfirmPassword);
            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                response => Results.Ok(response));
        }

        /// <summary>Chọn tenant cho phiên đăng nhập.</summary>
        /// <remarks>
        /// Kiểm tra selection token và tenant membership đang hoạt động, sau đó
        /// phát access token chứa ngữ cảnh tenant đã chọn.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("select-tenant")]
        public async Task<IResult> SelectTenant(
            [FromBody] SelectTenantRequest request,
            CancellationToken cancellationToken)
        {
            var command = new SelectTenantCommand(
                request.SelectionToken,
                request.TenantId);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                response => Results.Ok(response));
        }
    }
}
