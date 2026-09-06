using AgriDrone.Api.Contracts.TenantMembership;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.Modules.Identity.Application.Features.TransferTenantOwnership;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[Route("api/tenants/current")]
[ApiController]
[Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
public sealed class TenantOwnershipController(ISender sender)
    : ControllerBase
{
    /// <summary>Chuyển quyền sở hữu tenant hiện tại.</summary>
    /// <remarks>
    /// Tenant Owner chuyển vai trò Owner cho một thành viên active trong cùng
    /// tenant. Owner cũ được hạ xuống Tenant Admin trong cùng transaction.
    /// </remarks>
    [HttpPost("transfer-ownership")]
    public async Task<IResult> TransferOwnership(
        [FromBody] TransferTenantOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransferTenantOwnershipCommand(
                request.NewOwnerUserId),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            () => Results.NoContent());
    }
}
