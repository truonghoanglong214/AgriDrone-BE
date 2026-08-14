using AgriDrone.Api.Contracts.Users;
using AgriDrone.Modules.Identity.Application.Features.RegisterUser;
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
    }
}
