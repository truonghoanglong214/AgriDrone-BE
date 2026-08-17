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
