using AgriDrone.Api.Contracts.Tenants;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.Modules.Identity.Application.Features.GetUserTenants;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[Route("api/system/users")]
[ApiController]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemUserTenantsController(ISender sender) : ControllerBase
{
    /// <summary>Lấy các tenant mà một người dùng tham gia.</summary>
    /// <remarks>
    /// System Admin truy vấn danh sách tenant membership của người dùng theo
    /// trang, bao gồm vai trò và trạng thái membership tương ứng.
    /// </remarks>
    [HttpGet("{userId:guid}/tenants")]
    public async Task<IResult> GetUserTenantsAsync(
        [FromRoute] Guid userId,
        [FromQuery] GetUserTenantsRequest request,
        CancellationToken cancellationToken)
    {
        var query = new GetUserTenantsQuery(
            userId,
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
