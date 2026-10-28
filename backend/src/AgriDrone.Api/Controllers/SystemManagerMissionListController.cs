using AgriDrone.Modules.Missions.Application.Features.Missions.GetMissions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/farms/{farmId:guid}/missions")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerMissionListController(
    ISender sender,
    ISystemManagerAccessService managerAccessService) : ControllerBase
{
    /// <summary>Lấy danh sách mission trong farm mà System Manager được phân công.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<MissionListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IResult> GetMissions(
        [FromRoute] Guid farmId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? zoneId = null,
        [FromQuery] Guid? droneId = null,
        [FromQuery] MissionType? missionType = null,
        [FromQuery] MissionStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != farmId)
            return Results.Forbid();

        var result = await sender.Send(
            new GetMissionsQuery(
                farmId,
                pageNumber,
                pageSize,
                zoneId,
                droneId,
                missionType,
                status,
                search,
                tenantId),
            cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
