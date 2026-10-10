using System.Text.Json;
using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetActivePreflightChecklist;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.SyncFieldNote;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetFieldNotes;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/farms/{farmId:guid}/missions/{missionId:guid}")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerMissionsController(ISender sender) : ControllerBase
{
    /// <remarks>Đồng bộ ghi chú hiện trường bằng OperationId để retry an toàn. Có thể ghi loại sự cố, kết quả xử lý, quyết định recovery và tham chiếu bằng chứng; quyết định phải phù hợp trạng thái chuyến bay.</remarks>
    [HttpPost("field-notes")]
    public async Task<IResult> SyncFieldNote(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromBody] SyncMissionFieldNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SyncFieldNoteCommand(
            farmId, missionId, request.OperationId, request.Text,
            request.ObservedAt, request.IncidentType, request.IncidentOutcome,
            request.RecoveryDecision, request.EvidenceReference), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Lấy ghi chú và sự cố đã ghi cho mission thuộc Farm được phân công.</remarks>
    [HttpGet("field-notes")]
    public async Task<IResult> GetFieldNotes(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFieldNotesQuery(
            farmId, missionId), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Đổi lịch mission Scheduled; với FlightFailed, chỉ phục hồi khi sự cố có quyết định RESCHEDULE_REQUIRED. Có thể chọn drone thay thế cho lần bay lại. ExpectedVersion chống cập nhật đồng thời và checklist cũ hết hiệu lực.</remarks>
    [HttpPost("reschedule")]
    public async Task<IResult> Reschedule(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromBody] RescheduleOrderMissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RescheduleOrderMissionCommand(
            farmId, missionId, request.ExpectedVersion,
            request.StartAt, request.EndAt, request.ReplacementDroneId), cancellationToken);
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

    /// <remarks>Lưu câu trả lời checklist theo OperationId và ExpectedVersion. Nếu xác nhận phù hợp để bay, cần ghi bằng chứng điều kiện cho phép bay và phương án failsafe; quyết định không bay được lưu để theo dõi.</remarks>
    [HttpPost("preflight")]
    public async Task<IResult> CompletePreflight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] CompleteMissionPreflightRequest request,
        CancellationToken cancellationToken)
    {
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
                request.OperationId,
                request.ExpectedVersion,
                request.ChecklistDefinitionId,
                request.ChecklistVersion,
                answers,
                request.SuitableForFlight,
                request.Notes,
                request.DeviceCompletedAt,
                request.UnsuitableConditionNotes,
                request.FailsafeNotes,
                request.FlightAuthorizationEvidence),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <remarks>Chỉ bắt đầu trong lịch bay và lịch hẹn đã xác nhận. Handler kiểm tra lại Order, boundary/scope, phân công, drone, checklist và các điều kiện riêng cho BaselineMapping hoặc dịch vụ trả phí.</remarks>
    [HttpPost("start")]
    public Task<IResult> Start(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] OperateMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.InFlight, request, cancellationToken);

    /// <remarks>Chuyển chuyến bay InFlight sang FlightCompleted và ghi thời điểm kết thúc. Bước upload media/telemetry được thực hiện qua API riêng.</remarks>
    [HttpPost("complete-flight")]
    public Task<IResult> CompleteFlight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] OperateMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.FlightCompleted, request, cancellationToken);

    /// <remarks>Ghi chuyến bay thất bại cùng Reason, loại sự cố, kết quả xử lý, quyết định recovery và tham chiếu bằng chứng trong một giao dịch; đồng thời chuyển drone sang bảo trì.</remarks>
    [HttpPost("fail-flight")]
    public Task<IResult> FailFlight(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] OperateMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.FlightFailed, request, cancellationToken);

    /// <remarks>Hủy mission ở trạng thái Draft hoặc Scheduled; yêu cầu Reason để lưu vết quyết định.</remarks>
    [HttpPost("cancel")]
    public Task<IResult> Cancel(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] OperateMissionRequest request,
        CancellationToken cancellationToken) =>
        Transition(farmId, missionId, MissionStatus.Cancelled, request, cancellationToken);

    private async Task<IResult> Transition(
        Guid farmId,
        Guid missionId,
        MissionStatus status,
        OperateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionMissionCommand(
                farmId,
                missionId,
                status,
                request.ExpectedVersion,
                request.Reason,
                request.IncidentOperationId,
                request.IncidentType,
                request.IncidentOutcome,
                request.RecoveryDecision,
                request.EvidenceReference),
            cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
