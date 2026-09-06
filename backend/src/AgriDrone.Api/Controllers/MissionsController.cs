using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application
    .Features.Missions.CreateMission;
using AgriDrone.Modules.Missions.Application
    .Features.Missions.GetMissionDetails;
using AgriDrone.Modules.Missions.Application
    .Features.Missions.ScheduleMission;
using AgriDrone.Modules.Missions.Application
    .Features.Missions.TransitionMission;
using AgriDrone.SharedInfrastructure.Authorization;
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
    /// <summary>Tạo Mission ở trạng thái Draft.</summary>
    /// <remarks>
    /// Farm Manager tạo Mission Mapping hoặc Health Inspection cho một Zone active.
    /// Health Inspection phải tham chiếu confirmed map của Zone; mã Mission là duy
    /// nhất trong Farm. Drone và các tham chiếu phải thuộc đúng tenant.
    /// </remarks>
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

    /// <summary>Lập lịch cho Mission Draft.</summary>
    /// <remarks>
    /// Chuyển Mission từ Draft sang Scheduled sau khi kiểm tra drone đang Available,
    /// không giao lịch và khoảng thời gian hợp lệ. ExpectedVersion bảo vệ khỏi cập
    /// nhật đồng thời.
    /// </remarks>
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

    /// <summary>Chuyển trạng thái chuyến bay của Mission.</summary>
    /// <remarks>
    /// Thực hiện các transition được phép: Scheduled sang InFlight, InFlight sang
    /// FlightCompleted hoặc FlightFailed, và Draft/Scheduled sang Cancelled. Khi
    /// bắt đầu hoặc kết thúc chuyến bay, trạng thái drone được cập nhật đồng bộ.
    /// </remarks>
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

    /// <summary>Lấy chi tiết Mission.</summary>
    /// <remarks>
    /// Trả toàn bộ thông tin Mission thuộc đúng tenant và farm, gồm Zone, drone,
    /// pilot, loại Mission, lịch bay, trạng thái, source map và version hiện tại.
    /// </remarks>
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
                AccessAuthorizationPolicies.FarmManage);

        return authorization.Succeeded
            ? null
            : Results.Forbid();
    }
}
