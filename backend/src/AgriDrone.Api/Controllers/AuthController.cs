using AgriDrone.Api.Contracts.Users;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.Modules.Identity.Application.Features.ForgotPassword;
using AgriDrone.Modules.Identity.Application.Features.LoginUser;
using AgriDrone.Modules.Identity.Application.Features.LogoutSession;
using AgriDrone.Modules.Identity.Application.Features.RefreshSession;
using AgriDrone.Modules.Identity.Application.Features.ResetPassword;
using AgriDrone.Modules.Identity.Application.Features.SelectTenant;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AgriDrone.Api.Controllers
{
    [Route("api/auth")]
    [ApiController]
    [EnableCors("Auth")]
    public class AuthController(
        ISender sender,
        IOptions<RefreshSessionOptions> refreshOptions,
        IConfiguration configuration) : ControllerBase
    {
        public const string SessionHeaderName = "X-AgriDrone-Session";
        public const string CookiePath = "/api/auth";

        private readonly RefreshSessionOptions _refreshOptions = refreshOptions.Value;

        /// <summary>Đăng nhập hệ thống.</summary>
        /// <remarks>
        /// Xác thực email và mật khẩu. Nếu người dùng chỉ thuộc một tenant hoặc có vai trò hệ thống thì
        /// trả access token; nếu thuộc nhiều tenant thì trả selection token và danh sách tenant.
        /// Gửi header <c>X-AgriDrone-Session: 1</c> để web frontend nhận cookie refresh <c>agridrone_refresh</c>.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType<LoginUserResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IResult> Login(
            [FromBody] LoginUserRequest request,
            CancellationToken cancellationToken)
        {
            var sessionContract = ValidateOptionalSessionRequest();
            if (sessionContract.Error is not null)
            {
                return sessionContract.Error;
            }

            var command = new LoginUserCommand(
                request.Email,
                request.Password,
                sessionContract.IssueRefreshCookie);
            var result = await sender.Send(
                command,
                cancellationToken);
            return result.ToHttpResult(
                HttpContext,
                outcome => WriteSessionResponse(outcome, StatusCodes.Status200OK));
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
        /// cho tài khoản đang hoạt động, đồng thời thu hồi toàn bộ phiên refresh hiện có.
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
        /// Kiểm tra selection token (kèm phiên bản xác thực của người dùng) và tenant membership đang hoạt động,
        /// sau đó phát access token chứa ngữ cảnh tenant đã chọn. Gửi <c>X-AgriDrone-Session: 1</c> để cấp cookie refresh.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("select-tenant")]
        [ProducesResponseType<LoginUserResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IResult> SelectTenant(
            [FromBody] SelectTenantRequest request,
            CancellationToken cancellationToken)
        {
            var sessionContract = ValidateOptionalSessionRequest();
            if (sessionContract.Error is not null)
            {
                return sessionContract.Error;
            }

            var command = new SelectTenantCommand(
                request.SelectionToken,
                request.TenantId,
                sessionContract.IssueRefreshCookie);

            var result = await sender.Send(command, cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                outcome => WriteSessionResponse(outcome, StatusCodes.Status200OK));
        }

        /// <summary>Làm mới access token và rotate cookie phiên web.</summary>
        /// <remarks>
        /// Yêu cầu header <c>X-AgriDrone-Session: 1</c>, <c>Origin</c> thuộc allowlist auth và cookie
        /// <c>agridrone_refresh</c>. Dùng được ngay cả khi access JWT đã hết hạn.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("refresh")]
        [ProducesResponseType<LoginUserResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IResult> Refresh(CancellationToken cancellationToken)
        {
            var guardError = ValidateRequiredCookieOriginAndHeader();
            if (guardError is not null)
            {
                return guardError;
            }

            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            Request.Cookies.TryGetValue(_refreshOptions.CookieName, out var rawRefreshToken);
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
            {
                return ToProblem(AuthenticationError.InvalidRefreshSession());
            }

            var result = await sender.Send(
                new RefreshSessionCommand(rawRefreshToken),
                cancellationToken);

            return result.ToHttpResult(
                HttpContext,
                outcome => WriteSessionResponse(outcome, StatusCodes.Status200OK));
        }

        /// <summary>Đăng xuất phiên web và thu hồi refresh session trên server.</summary>
        /// <remarks>
        /// Yêu cầu header <c>X-AgriDrone-Session: 1</c> và <c>Origin</c> thuộc allowlist auth.
        /// Luôn xóa cookie refresh và trả <c>204 No Content</c>, kể cả khi phiên đã đăng xuất trước đó.
        /// </remarks>
        [AllowAnonymous]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IResult> Logout(CancellationToken cancellationToken)
        {
            var guardError = ValidateRequiredCookieOriginAndHeader();
            if (guardError is not null)
            {
                return guardError;
            }

            Request.Cookies.TryGetValue(_refreshOptions.CookieName, out var rawRefreshToken);
            await sender.Send(
                new LogoutSessionCommand(rawRefreshToken),
                cancellationToken);

            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";
            Response.Cookies.Delete(_refreshOptions.CookieName, CreateCookieOptions(expiresAt: null));

            return Results.NoContent();
        }

        private (bool IssueRefreshCookie, IResult? Error) ValidateOptionalSessionRequest()
        {
            if (!Request.Headers.TryGetValue(SessionHeaderName, out var headerValues))
            {
                return (false, null);
            }

            if (!IsValidSessionHeaderValue(headerValues))
            {
                return (false, ToProblem(AuthenticationError.SessionHeaderRequired()));
            }

            if (Request.Headers.TryGetValue("Origin", out var originValues) &&
                !IsAllowedOrigin(originValues))
            {
                return (false, ToProblem(AuthenticationError.InvalidOrigin()));
            }

            return (true, null);
        }

        private IResult? ValidateRequiredCookieOriginAndHeader()
        {
            if (!Request.Headers.TryGetValue(SessionHeaderName, out var headerValues) ||
                !IsValidSessionHeaderValue(headerValues))
            {
                return ToProblem(AuthenticationError.SessionHeaderRequired());
            }

            if (!Request.Headers.TryGetValue("Origin", out var originValues) ||
                !IsAllowedOrigin(originValues))
            {
                return ToProblem(AuthenticationError.InvalidOrigin());
            }

            return null;
        }

        private static bool IsValidSessionHeaderValue(StringValues values) =>
            values.Count == 1 &&
            string.Equals(values[0]?.Trim(), "1", StringComparison.Ordinal);

        private bool IsAllowedOrigin(StringValues originValues)
        {
            if (originValues.Count != 1)
            {
                return false;
            }

            var normalizedRequestOrigin = NormalizeOrigin(originValues[0]);
            if (normalizedRequestOrigin is null)
            {
                return false;
            }

            var allowedOrigins = ResolveAllowedOrigins();
            return allowedOrigins.Contains(normalizedRequestOrigin, StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> ResolveAllowedOrigins()
        {
            var configured = _refreshOptions.AllowedOrigins is { Length: > 0 }
                ? _refreshOptions.AllowedOrigins
                : configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in configured)
            {
                var normalized = NormalizeOrigin(candidate);
                if (normalized is not null)
                {
                    set.Add(normalized);
                }
            }

            return set;
        }

        private static string? NormalizeOrigin(string? rawOrigin)
        {
            if (string.IsNullOrWhiteSpace(rawOrigin))
            {
                return null;
            }

            if (!Uri.TryCreate(rawOrigin.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.PathAndQuery.TrimEnd('/')) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                return null;
            }

            return uri.GetLeftPart(UriPartial.Authority);
        }

        private IResult WriteSessionResponse(AuthenticationCommandResult outcome, int statusCode)
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            if (outcome.RefreshCookie is { } cookie)
            {
                Response.Cookies.Append(
                    _refreshOptions.CookieName,
                    cookie.RawToken,
                    CreateCookieOptions(cookie.ExpiresAt));
            }

            return Results.Json(outcome.Payload, statusCode: statusCode);
        }

        private CookieOptions CreateCookieOptions(DateTimeOffset? expiresAt) => new()
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
            IsEssential = true,
            Expires = expiresAt
        };

        private IResult ToProblem(SharedKernel.Application.AppError error)
        {
            return SharedKernel.Application.Result.Failure(error).ToHttpResult(
                HttpContext,
                () => Results.Ok());
        }
    }
}
