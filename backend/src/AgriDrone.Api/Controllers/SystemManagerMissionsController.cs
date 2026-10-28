using System.Globalization;
using System.Text.Json;
using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetActivePreflightChecklist;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetMissionDetails;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/farms/{farmId:guid}/missions/{missionId:guid}")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerMissionsController(
    ISender sender,
    ISystemManagerAccessService managerAccessService,
    IMissionPlantGpsStore plantGpsStore,
    IMissionPlantGpsPublisher plantGpsPublisher,
    IExecutionContext executionContext) : ControllerBase
{
    /// <summary>Lấy danh sách cây CSV đã lưu cho mission.</summary>
    [HttpGet("plant-gps")]
    public async Task<IResult> GetPlantGps(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != farmId)
            return Results.Forbid();

        var rows = await plantGpsStore.GetAsync(
            tenantId, farmId, missionId, cancellationToken);
        if (rows is null)
            return Results.NotFound();

        return Results.Ok(new
        {
            missionId,
            savedCount = rows.Count,
            trees = rows.Select(tree => new
            {
                tree.TreeId,
                tree.Row,
                tree.Col,
                tree.Latitude,
                tree.Longitude,
                tree.Confidence,
                tree.Status,
                tree.Source
            }).ToArray()
        });
    }

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

    /// <summary>Lưu danh sách cây từ file CSV cho mission.</summary>
    [HttpPost("plant-gps/csv")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IResult> ReplacePlantGpsFromCsv(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        const long maximumFileSizeBytes = 5 * 1024 * 1024;
        const string expectedHeader =
            "tree_id,row,col,latitude,longitude,confidence,status,source";

        if (file is null || file.Length == 0 ||
            file.Length > maximumFileSizeBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Upload a non-empty CSV file no larger than 5 MB."]
            });
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is null || access.FarmId != farmId)
            return Results.Forbid();

        var rows = new List<MissionPlantGpsRow>();
        using var reader = new StreamReader(file.OpenReadStream());
        var header = await reader.ReadLineAsync(cancellationToken);

        if (!string.Equals(header?.TrimStart('\uFEFF'), expectedHeader,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [$"CSV header must be: {expectedHeader}."]
            });
        }

        var lineNumber = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Python currently emits unquoted scalar values with this exact 8-column schema.
            var columns = line.Split(',');
            if (columns.Length != 8 ||
                !int.TryParse(columns[1], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var row) ||
                !int.TryParse(columns[2], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var col) ||
                !decimal.TryParse(columns[3], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var latitude) ||
                !decimal.TryParse(columns[4], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var longitude) ||
                !decimal.TryParse(columns[5], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var confidence))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = [$"CSV row {lineNumber} has an invalid format."]
                });
            }

            rows.Add(new MissionPlantGpsRow(
                columns[0].Trim(), row, col, latitude, longitude,
                confidence, columns[6].Trim(), columns[7].Trim()));

            if (rows.Count > 10000)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["CSV cannot contain more than 10000 data rows."]
                });
            }
        }

        if (rows.Count == 0 ||
            rows.Any(tree =>
                string.IsNullOrWhiteSpace(tree.TreeId) || tree.TreeId.Length > 100 ||
                tree.Row < 1 || tree.Col < 1 ||
                tree.Latitude is < -90 or > 90 ||
                tree.Longitude is < -180 or > 180 ||
                tree.Confidence is < 0 or > 1 ||
                tree.Status is not ("DETECTED" or "MISSING") ||
                string.IsNullOrWhiteSpace(tree.Source) || tree.Source.Length > 100) ||
            rows.Select(tree => tree.TreeId)
                .Distinct(StringComparer.Ordinal).Count() != rows.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] =
                ["CSV must contain valid rows with unique tree_id values, valid coordinates, and status DETECTED or MISSING."]
            });
        }

        var saved = await plantGpsStore.ReplaceAsync(
            access.TenantId.Value, farmId, missionId, rows, cancellationToken);

        return saved
            ? Results.Ok(new { missionId, savedCount = rows.Count })
            : Results.NotFound();
    }

    /// <summary>Ghép dữ liệu CSV đã lưu với metadata V3 và xếp message vào Outbox.</summary>
    /// <remarks>
    /// Request body chứa metadata payload cần để dựng mapping.baseline-candidates-ready.v3.
    /// API lấy treeId trong payload để ghép với các dòng CSV đã lưu; tọa độ, hàng,
    /// cột và confidence trong message lấy từ DB. Message envelope
    /// được tạo phía server. Dùng lại Idempotency-Key nếu retry cùng một lần publish.
    /// </remarks>
    [HttpPost("plant-gps/publish")]
    public async Task<IResult> PublishPlantGps(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        [FromBody] PublishMissionPlantGpsRequest request,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is not Guid messageId || messageId == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["A non-empty GUID Idempotency-Key is required."]
            });
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != farmId)
            return Results.Forbid();

        if (executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
            return Results.Unauthorized();

        if (request.Payload is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["payload"] = ["A payload is required."]
            });
        }

        if (request.Payload.Zones is null || request.Payload.Zones.Count == 0 ||
            request.Payload.Zones.Any(zone => zone is null ||
                zone.Candidates is null || zone.Candidates.Count == 0 ||
                zone.Candidates.Any(candidate => candidate is null ||
                    string.IsNullOrWhiteSpace(candidate.TreeId))))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["payload.zones"] = ["At least one zone with candidate rows is required."]
            });
        }

        var payload = new BaselineMappingCandidatesReadyV3(
            request.Payload.SurveyOrderId,
            missionId,
            farmId,
            request.Payload.Zones.Select(zone => new ZoneMappingCandidatesV3(
                zone.ZoneId,
                zone.Candidates.Select(candidate =>
                    new MappingCandidateV3(TreeId: candidate.TreeId)).ToArray(),
                GridBearingDeg: zone.GridBearingDeg,
                RowSpacingM: zone.RowSpacingM,
                PlantSpacingM: zone.PlantSpacingM)).ToArray());

        var result = await plantGpsPublisher.PublishAsync(
            tenantId,
            farmId,
            missionId,
            actorId,
            executionContext.CorrelationId,
            messageId,
            payload,
            cancellationToken);

        return result.Status switch
        {
            MissionPlantGpsPublishStatus.Queued => Results.Ok(new
            {
                missionId,
                messageId = result.MessageId,
                status = "QUEUED"
            }),
            MissionPlantGpsPublishStatus.NotFound => Results.NotFound(),
            MissionPlantGpsPublishStatus.Conflict => Results.Conflict(new
            {
                errors = result.Errors
            }),
            MissionPlantGpsPublishStatus.Invalid => Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["payload"] = result.Errors?.ToArray() ?? ["Payload is invalid."]
                }),
            _ => Results.Problem("Unable to queue the plant GPS message.")
        };
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
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != farmId)
            return Results.Forbid();

        var result = await sender.Send(
            new GetMissionDetailsQuery(tenantId, farmId, missionId),
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
