using System.Globalization;
using System.Text.Json;
using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Api.Contracts.Farms;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Farms.Application.Features.GetAssignedFarmOperationalContext;
using AgriDrone.Modules.Farms.Application.Features.VerifyFarmBoundary;
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
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

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

    /// <summary>Lưu cây GPS và tùy chọn đồng bộ Farm boundary bằng JSON.</summary>
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
        var boundaryPoints = request.Boundary;
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

        Polygon? boundary = null;
        var boundaryPointCount = 0;
        if (boundaryPoints is { Count: > 0 })
        {
            if (boundaryPoints.Any(point =>
                    point.Longitude is < -180 or > 180 ||
                    point.Latitude is < -90 or > 90) ||
                boundaryPoints.DistinctBy(point =>
                    (point.Longitude, point.Latitude)).Count() < 3)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["boundary"] = ["Provide at least three distinct valid WGS84 boundary points."]
                });
            }

            var positions = boundaryPoints
                .Select(point => new[]
                {
                    (double)point.Longitude,
                    (double)point.Latitude
                })
                .ToList();
            var first = positions[0];
            var last = positions[^1];
            var isClosed = first[0] == last[0] && first[1] == last[1];
            if (!isClosed)
            {
                positions.Add([first[0], first[1]]);
            }

            boundaryPointCount = isClosed
                ? boundaryPoints.Count - 1
                : boundaryPoints.Count;
            boundary = GeoJsonGeometryMapper.ToPolygon(
                new GeoJsonPolygonRequest(
                    "Polygon",
                    [positions.ToArray()]));

            if (boundary is null || !boundary.IsValid ||
                boundary.IsEmpty || boundary.Area <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["boundary"] = ["Boundary points do not form a valid WGS84 polygon."]
                });
            }

            var outsideTreeIds = trees
                .Where(tree => !boundary.Covers(
                    boundary.Factory.CreatePoint(new Coordinate(
                        (double)tree.Longitude!.Value,
                        (double)tree.Latitude!.Value))))
                .Select(tree => tree.TreeId)
                .Take(10)
                .ToArray();
            if (outsideTreeIds.Length > 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["trees"] =
                    [$"Every tree must be covered by the boundary. Outside treeId values: {string.Join(", ", outsideTreeIds)}."]
                });
            }
        }

        var missionRows = await plantGpsStore.GetAsync(
            access.TenantId!.Value, farmId, missionId, cancellationToken);
        if (missionRows is null)
            return Results.NotFound();

        Guid? boundaryId = null;
        int? boundaryVersionNumber = null;
        if (boundary is not null)
        {
            var contextResult = await sender.Send(
                new GetAssignedFarmOperationalContextQuery(farmId),
                cancellationToken);
            if (contextResult.IsFailure)
            {
                return contextResult.ToHttpResult(
                    HttpContext,
                    _ => Results.NoContent());
            }

            var operationalContext = contextResult.Value;
            var currentBoundary = operationalContext.ApprovedBoundary;
            if (currentBoundary is not null &&
                currentBoundary.Geometry.EqualsTopologically(boundary))
            {
                boundaryId = currentBoundary.Id;
                boundaryVersionNumber = currentBoundary.VersionNumber;
            }
            else
            {
                var boundaryResult = await sender.Send(
                    new VerifyFarmBoundaryCommand(
                        farmId,
                        boundary,
                        "Update Farm boundary from mission plant GPS JSON.",
                        $"mission:{missionId:D}",
                        operationalContext.Version,
                        currentBoundary?.VersionNumber),
                    cancellationToken);
                if (boundaryResult.IsFailure)
                {
                    return boundaryResult.ToHttpResult(
                        HttpContext,
                        _ => Results.NoContent());
                }

                boundaryId = boundaryResult.Value.BoundaryId;
                boundaryVersionNumber = boundaryResult.Value.VersionNumber;
            }
        }

        var rows = trees.Select(tree => new MissionPlantGpsRow(
            tree.TreeId, tree.Row, tree.Col, tree.Latitude!.Value, tree.Longitude!.Value,
            tree.Confidence, tree.Status, tree.Source)).ToArray();
        var saved = await plantGpsStore.ReplaceAsync(access.TenantId.Value,
            farmId, missionId, rows, cancellationToken);
        return saved
            ? Results.Ok(new
            {
                missionId,
                savedCount = rows.Length,
                boundaryPointCount,
                boundaryId,
                boundaryVersionNumber,
                boundaryStatus = boundaryId.HasValue ? "Approved" : null
            })
            : Results.NotFound();
    }

    /// <summary>
    /// Lưu cây và cập nhật Farm boundary từ các dòng BOUNDARY trong CSV.
    /// </summary>
    [HttpPost("plant-gps/csv")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IResult> ReplacePlantGpsFromCsv(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromForm] UploadMissionPlantGpsCsvRequest request,
        CancellationToken cancellationToken)
    {
        var file = request.File;
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
        var boundaryPositions = new List<double[]>();
        var boundaryPointIds = new HashSet<string>(StringComparer.Ordinal);
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
        var dataRowCount = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            dataRowCount++;

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

            var treeId = columns[0].Trim();
            var status = columns[6].Trim();
            var source = columns[7].Trim();

            if (string.Equals(status, "BOUNDARY", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(treeId) || treeId.Length > 100 ||
                    !boundaryPointIds.Add(treeId) || row != 0 || col != 0 ||
                    latitude is < -90 or > 90 ||
                    longitude is < -180 or > 180 ||
                    confidence is < 0 or > 1 ||
                    string.IsNullOrWhiteSpace(source) || source.Length > 100)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["file"] =
                        [$"CSV boundary row {lineNumber} must have a unique tree_id, row/col 0, valid coordinates, confidence, and source."]
                    });
                }

                boundaryPositions.Add([(double)longitude, (double)latitude]);
            }
            else
            {
                rows.Add(new MissionPlantGpsRow(
                    treeId, row, col, latitude, longitude,
                    confidence, status, source));
            }

            if (dataRowCount > 10000)
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

        Polygon? csvBoundary = null;
        if (boundaryPositions.Count > 0)
        {
            if (boundaryPositions.Count < 3 ||
                boundaryPositions.DistinctBy(position =>
                    (position[0], position[1])).Count() < 3)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] =
                    ["CSV boundary must contain at least three distinct coordinate points."]
                });
            }

            var first = boundaryPositions[0];
            var last = boundaryPositions[^1];
            if (first[0] != last[0] || first[1] != last[1])
            {
                boundaryPositions.Add([first[0], first[1]]);
            }

            csvBoundary = GeoJsonGeometryMapper.ToPolygon(
                new GeoJsonPolygonRequest(
                    "Polygon",
                    [boundaryPositions.ToArray()]));

            if (csvBoundary is null || !csvBoundary.IsValid ||
                csvBoundary.IsEmpty || csvBoundary.Area <= 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] =
                    ["CSV boundary points do not form a valid WGS84 polygon."]
                });
            }

            var outsideTreeIds = rows
                .Where(tree => !csvBoundary.Covers(
                    csvBoundary.Factory.CreatePoint(new Coordinate(
                        (double)tree.Longitude,
                        (double)tree.Latitude))))
                .Select(tree => tree.TreeId)
                .Take(10)
                .ToArray();

            if (outsideTreeIds.Length > 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] =
                    [$"Every tree must be covered by the CSV boundary. Outside tree_id values: {string.Join(", ", outsideTreeIds)}."]
                });
            }
        }

        var missionRows = await plantGpsStore.GetAsync(
            access.TenantId.Value, farmId, missionId, cancellationToken);
        if (missionRows is null)
            return Results.NotFound();

        Guid? boundaryId = null;
        int? boundaryVersionNumber = null;
        if (csvBoundary is not null)
        {
            var contextResult = await sender.Send(
                new GetAssignedFarmOperationalContextQuery(farmId),
                cancellationToken);
            if (contextResult.IsFailure)
            {
                return contextResult.ToHttpResult(
                    HttpContext,
                    _ => Results.NoContent());
            }

            var operationalContext = contextResult.Value;
            var currentBoundary = operationalContext.ApprovedBoundary;
            if (currentBoundary is not null &&
                currentBoundary.Geometry.EqualsTopologically(csvBoundary))
            {
                boundaryId = currentBoundary.Id;
                boundaryVersionNumber = currentBoundary.VersionNumber;
            }
            else
            {
                var boundaryResult = await sender.Send(
                    new VerifyFarmBoundaryCommand(
                        farmId,
                        csvBoundary,
                        "Update Farm boundary from mission plant GPS CSV.",
                        $"mission:{missionId:D}",
                        operationalContext.Version,
                        currentBoundary?.VersionNumber),
                    cancellationToken);
                if (boundaryResult.IsFailure)
                {
                    return boundaryResult.ToHttpResult(
                        HttpContext,
                        _ => Results.NoContent());
                }

                boundaryId = boundaryResult.Value.BoundaryId;
                boundaryVersionNumber = boundaryResult.Value.VersionNumber;
            }
        }

        var saved = await plantGpsStore.ReplaceAsync(
            access.TenantId.Value, farmId, missionId, rows, cancellationToken);

        return saved
            ? Results.Ok(new
            {
                missionId,
                savedCount = rows.Count,
                boundaryPointCount = boundaryPositions.Count == 0
                    ? 0
                    : boundaryPositions.Count - 1,
                boundaryId,
                boundaryVersionNumber,
                boundaryStatus = boundaryId.HasValue ? "Approved" : null
            })
            : Results.NotFound();
    }

    /// <summary>Kiểm tra candidate GPS đầy đủ và xếp message vào Outbox.</summary>
    /// <remarks>
    /// Client gửi SurveyOrderId, spacing và candidate GPS đầy đủ, không cần gửi zone.
    /// Các candidate phải khớp những dòng đã lưu trong mission. Backend dùng hoặc
    /// gán zone nội bộ rồi tạo message. Dùng lại Idempotency-Key khi retry.
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

        if (request.Payload.Candidates is null || request.Payload.Candidates.Count == 0 ||
            request.Payload.Candidates.Any(candidate => candidate is null ||
                string.IsNullOrWhiteSpace(candidate.TreeId)))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["payload.candidates"] = ["At least one candidate with treeId is required."]
            });
        }

        var publishRequest = new MissionPlantGpsPublishRequest(
            request.Payload.SurveyOrderId,
            request.Payload.RowSpacingM,
            request.Payload.PlantSpacingM,
            request.Payload.GridBearingDeg,
            request.Payload.Candidates.Select(candidate =>
                new MissionPlantGpsCandidateInput(
                    candidate.TreeId,
                    candidate.Row,
                    candidate.Col,
                    candidate.Latitude,
                    candidate.Longitude,
                    candidate.Confidence,
                    candidate.Status,
                    candidate.Source)).ToArray());

        var result = await plantGpsPublisher.PublishAsync(
            tenantId,
            farmId,
            missionId,
            actorId,
            executionContext.CorrelationId,
            messageId,
            publishRequest,
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
