using AgriDrone.Api.Contracts.Users;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Identity.Application.Features.GetUsers;
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
        [Authorize(Policy = IdentityAuthorizationPolicies.SystemAdmin)]
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


    }
}
