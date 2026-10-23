using System.Text.Json;
using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetActivePreflightChecklist;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetMissionDetails;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/farms/{farmId:guid}/missions/{missionId:guid}")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerMissionsController(
    ISender sender,
    ISystemManagerAccessService managerAccessService,
    IMissionPlantGpsStore plantGpsStore) : ControllerBase
{
    /// <summary>Lưu bản CSV cây hiện tại theo mission trước khi tải xuống.</summary>
    [HttpPost("plant-gps")]
    public async Task<IResult> ReplacePlantGps(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromBody] ReplaceMissionPlantGpsRequest request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is null || access.FarmId != farmId)
            return Results.Forbid();

        var trees = request.Trees;
        if (trees is null || trees.Count is < 1 or > 10000 ||
            trees.Any(tree => string.IsNullOrWhiteSpace(tree.TreeId) || tree.TreeId.Length > 100 ||
                tree.Row < 1 || tree.Col < 1 ||
                tree.Latitude is null or < -90 or > 90 ||
                tree.Longitude is null or < -180 or > 180 ||
                tree.Confidence is < 0 or > 1 ||
                tree.Status is not ("DETECTED" or "MISSING") ||
                string.IsNullOrWhiteSpace(tree.Source) || tree.Source.Length > 100) ||
            trees.Select(tree => tree.TreeId).Distinct(StringComparer.Ordinal).Count() != trees.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["trees"] = ["Provide 1–10000 valid, uniquely identified tree rows."]
            });
        }

        var rows = trees.Select(tree => new MissionPlantGpsRow(
            tree.TreeId, tree.Row, tree.Col, tree.Latitude!.Value, tree.Longitude!.Value,
            tree.Confidence, tree.Status, tree.Source)).ToArray();
        var saved = await plantGpsStore.ReplaceAsync(access.TenantId.Value,
            farmId, missionId, rows, cancellationToken);
        return saved ? Results.Ok(new { missionId, savedCount = rows.Length })
            : Results.NotFound();
    }

    /// <remarks>Đổi ngày bay cho mission Scheduled và có thể thay Drone nếu Drone cũ hỏng. Với FlightFailed, chỉ phục hồi khi sự cố có quyết định RESCHEDULE_REQUIRED. ReplacementDroneId có thể bỏ qua để giữ Drone hiện tại. ExpectedVersion chống cập nhật đồng thời và checklist cũ hết hiệu lực.</remarks>
    [HttpPost("reschedule")]
    public async Task<IResult> Reschedule(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromBody] RescheduleOrderMissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RescheduleOrderMissionCommand(
            farmId, missionId, request.ExpectedVersion,
            request.ScheduledDate, request.ReplacementDroneId), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Lấy phiên bản checklist đang áp dụng cho mission trước khi SystemManager hoàn tất kiểm tra trước bay.</remarks>
    [HttpGet("preflight/checklist")]
    public async Task<IResult> GetPreflightChecklist(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetActivePreflightChecklistQuery(farmId, missionId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new ActivePreflightChecklistResponse(
                value.DefinitionId,
                value.Code,
                value.VersionNumber,
                value.Items)));
    }

    /// <summary>Lấy version hiện tại của Mission trong luồng BE2.</summary>
    /// <remarks>
    /// FE dùng version này làm ExpectedMissionVersion cho thao tác media và
    /// telemetry. Hai loại dữ liệu đó dùng version của Mission, không có row
    /// version riêng.
    /// </remarks>
    [HttpGet("version")]
    public async Task<IResult> GetMissionVersion(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            farmId, cancellationToken);
        if (!access.IsAllowed || access.FarmId != farmId)
            return Results.Forbid();

        var result = await sender.Send(
            new GetMissionDetailsQuery(farmId, missionId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            mission => Results.Ok(new { mission.Id, mission.Version }));
    }

    /// <remarks>Lưu câu trả lời checklist theo Idempotency-Key header, ExpectedMissionVersion và ChecklistVersion. FE giữ nguyên header key khi retry cùng lần gửi checklist. Nếu xác nhận phù hợp để bay, các câu trả lời checklist phải đạt điều kiện cấu hình.</remarks>
    [HttpPost("preflight")]
    public async Task<IResult> CompletePreflight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] CompleteMissionPreflightRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["A non-empty UUID idempotency key is required."]
            });
        }

        if (request.Answers.ValueKind != JsonValueKind.Object)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["answers"] = ["Checklist answers must be a JSON object."]
            });
        }

        using var answers = JsonDocument.Parse(request.Answers.GetRawText());
        var result = await sender.Send(
            new CompleteMissionPreflightCommand(
                farmId,
                missionId,
                idempotencyKey,
                request.ExpectedMissionVersion,
                request.ChecklistDefinitionId,
                request.ChecklistVersion,
                answers,
                request.SuitableForFlight,
                request.Notes,
                request.DeviceCompletedAt),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Chỉ bắt đầu trong lịch bay và lịch hẹn đã xác nhận. Handler kiểm tra lại Order, boundary/scope, phân công, drone, checklist và các điều kiện riêng cho BaselineMapping hoặc dịch vụ trả phí.</remarks>
    [HttpPost("start")]
    public Task<IResult> Start(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] StartMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.InFlight,
            request.ExpectedMissionVersion, null, cancellationToken);

    /// <remarks>Chuyển chuyến bay InFlight sang FlightCompleted và ghi thời điểm kết thúc. Bước upload media/telemetry được thực hiện qua API riêng.</remarks>
    [HttpPost("complete-flight")]
    public Task<IResult> CompleteFlight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] CompleteFlightMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.FlightCompleted,
            request.ExpectedMissionVersion, null, cancellationToken);

    /// <remarks>Ghi chuyến bay thất bại cùng Idempotency-Key header, Reason, loại sự cố, kết quả xử lý, quyết định recovery và tham chiếu bằng chứng trong một giao dịch; đồng thời chuyển drone sang bảo trì.</remarks>
    [HttpPost("fail-flight")]
    public async Task<IResult> FailFlight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] FailFlightMissionRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["A non-empty UUID idempotency key is required."]
            });
        }

        return await Transition(farmId, missionId, MissionStatus.FlightFailed,
            request.ExpectedMissionVersion, request.Reason, cancellationToken,
            idempotencyKey, request.IncidentType,
            request.IncidentOutcome, request.RecoveryDecision,
            request.EvidenceReference);
    }

    /// <remarks>Hủy mission ở trạng thái Draft hoặc Scheduled; yêu cầu Reason để lưu vết quyết định.</remarks>
    [HttpPost("cancel")]
    public Task<IResult> Cancel(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] CancelMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.Cancelled,
            request.ExpectedMissionVersion, request.Reason, cancellationToken);

    private async Task<IResult> Transition(
        Guid farmId,
        Guid missionId,
        MissionStatus status,
        uint expectedMissionVersion,
        string? reason,
        CancellationToken cancellationToken,
        Guid? incidentOperationId = null,
        string? incidentType = null,
        string? incidentOutcome = null,
        string? recoveryDecision = null,
        string? evidenceReference = null)
    {
        var result = await sender.Send(
            new TransitionMissionCommand(
                farmId,
                missionId,
                status,
                expectedMissionVersion,
                reason,
                incidentOperationId,
                incidentType,
                incidentOutcome,
                recoveryDecision,
                evidenceReference),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
