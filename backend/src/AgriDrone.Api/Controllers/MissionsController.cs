using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Missions.Application
    .Features.Missions.CreateMission;
using AgriDrone.Modules.Missions.Application
    .Features.Missions.GetMissionDetails;
using AgriDrone.Modules.Missions.Application
    .Features.Missions.ScheduleMission;
using AgriDrone.Modules.Missions.Application
    .Features.Missions.TransitionMission;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application
    .Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Authorize]
public sealed class MissionsController(
    ISender sender,
    IAuthorizationService authorizationService,
    ICurrentTenant currentTenant)
    : ControllerBase
{
    [HttpPost("api/farms/{farmId:guid}/missions")]
    public async Task<IResult> CreateMission(
        Guid farmId,
        [FromBody] CreateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var authorization =
            await AuthorizeFarmAsync(farmId);

        if (authorization is not null)
        {
            return authorization;
        }

        var command = new CreateMissionCommand(
            currentTenant.TenantId!.Value,
            farmId,
            request.ZoneId,
            request.DroneId,
            request.PilotUserId,
            request.MissionCode,
            request.MissionType,
            request.SourceMapVersionId,
            request.FlightParameters,
            request.Notes);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            mission => Results.Created(
                $"/api/farms/{farmId}/missions/{mission.Id}",
                mission));
    }

    [HttpPatch(
        "api/farms/{farmId:guid}/missions/" +
        "{missionId:guid}/schedule")]
    public async Task<IResult> ScheduleMission(
        Guid farmId,
        Guid missionId,
        [FromBody] ScheduleMissionRequest request,
        CancellationToken cancellationToken)
    {
        var authorization =
            await AuthorizeFarmAsync(farmId);

        if (authorization is not null)
        {
            return authorization;
        }

        var command = new ScheduleMissionCommand(
            currentTenant.TenantId!.Value,
            farmId,
            missionId,
            request.ScheduledAt,
            request.ScheduledEndAt,
            request.ExpectedVersion);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            Results.Ok);
    }

    [HttpPatch(
        "api/farms/{farmId:guid}/missions/" +
        "{missionId:guid}/status")]
    public async Task<IResult> TransitionMission(
        Guid farmId,
        Guid missionId,
        [FromBody] TransitionMissionRequest request,
        CancellationToken cancellationToken)
    {
        var authorization =
            await AuthorizeFarmAsync(farmId);

        if (authorization is not null)
        {
            return authorization;
        }

        var command = new TransitionMissionCommand(
            currentTenant.TenantId!.Value,
            farmId,
            missionId,
            request.TargetStatus,
            request.ExpectedVersion,
            request.Reason);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            Results.Ok);
    }

    [HttpGet(
        "api/farms/{farmId:guid}/missions/" +
        "{missionId:guid}")]
    public async Task<IResult> GetMissionDetails(
        Guid farmId,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var authorization =
            await AuthorizeFarmAsync(farmId);

        if (authorization is not null)
        {
            return authorization;
        }

        var query = new GetMissionDetailsQuery(
            currentTenant.TenantId!.Value,
            farmId,
            missionId);

        var result = await sender.Send(
            query,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            Results.Ok);
    }

    private async Task<IResult?> AuthorizeFarmAsync(
        Guid farmId)
    {
        if (currentTenant.TenantId is not Guid tenantId)
        {
            return Results.Unauthorized();
        }

        var authorization =
            await authorizationService.AuthorizeAsync(
                User,
                new FarmAccessTarget(
                    tenantId,
                    farmId),
                IdentityAuthorizationPolicies.FarmManager);

        return authorization.Succeeded
            ? null
            : Results.Forbid();
    }
}
