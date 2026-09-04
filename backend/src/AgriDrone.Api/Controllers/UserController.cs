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
