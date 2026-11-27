using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Modules.Surveys.Application.Features.Orders.MarkFarmBoundaryOnOrthophoto;
using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Api.Mapping;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.GetManagerSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetManagerSurveyOrderCaptureAttempts;
using AgriDrone.Modules.Surveys.Application.Features.Results.GetSurveyResultReview;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/system-manager/survey-orders")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class V3SystemManagerSurveyOrdersController(
    ISender sender,
    ISurveyCaptureExecutionCoordinator captureCoordinator,
    IExecutionContext executionContext,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet("{orderId:guid}/drones/available")]
    [ProducesResponseType<AvailableDronesForOrderResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> GetAvailableCaptureDrones(
        Guid orderId, [FromQuery] DateTimeOffset? startAt, [FromQuery] DateTimeOffset? endAt,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId)
            return Results.Forbid();

        if ((startAt is null) != (endAt is null) || endAt <= startAt)
            return Results.BadRequest("Provide both startAt and endAt in chronological order for the new capture window.");

        var selection = await captureCoordinator.GetDroneSelectionAsync(
            orderId, actorId, startAt, endAt, cancellationToken);
        if (selection is null)
            return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "A Capture scheduling window is unavailable for this order.");

        var result = await sender.Send(new GetAvailableDronesQuery(
            selection.FarmId, selection.StartAt, selection.EndAt), cancellationToken);
        return result.ToHttpResult(HttpContext, drones => Results.Ok(new AvailableDronesForOrderResponse(
            orderId, selection.FarmId, nameof(MissionPurpose.Capture), selection.StartAt, selection.EndAt,
            drones.Where(drone =>
                (!selection.MapsPoles || DroneCapabilityPolicy.Supports(drone.Specifications, MissionPurpose.BaselineMapping)) &&
                (!selection.ScreensDisease || DroneCapabilityPolicy.Supports(drone.Specifications, MissionPurpose.PlantHealth))).ToArray())));
    }

    [HttpPost("{orderId:guid}/capture-attempts/{attemptId:guid}/farm-boundary/mark-orthophoto-corners")]
    [ProducesResponseType<MarkFarmBoundaryOnOrthophotoResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> MarkCaptureFarmBoundary(
        Guid orderId, Guid attemptId, [FromBody] MarkCaptureOrthophotoCornersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MarkFarmBoundaryOnOrthophotoCommand(
            orderId, request.Corners, request.ExpectedOrderVersion, request.Reason,
            CaptureAttemptId: attemptId, ExpectedMissionVersion: request.ExpectedMissionVersion,
            ExpectedBoundaryVersionNumber: request.ExpectedBoundaryVersionNumber), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    [HttpPost("{orderId:guid}/capture-appointments")]
    [ProducesResponseType<SurveyAppointmentApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ProposeCaptureAppointment(
        [FromRoute] Guid orderId,
        [FromBody] ProposeV3CaptureAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ScheduledDate is not DateOnly scheduledDate ||
            scheduledDate == default)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ScheduledDate)] =
                    ["A valid scheduled date is required."]
            });
        }

        if (!SurveyAppointmentWindow.TryResolveForDate(
                scheduledDate,
                timeProvider.GetUtcNow(),
                out var proposedStartAt,
                out var proposedEndAt,
                out var errorMessage))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ScheduledDate)] =
                    [errorMessage ?? "A valid scheduled date is required."]
            });
        }

        var result = await sender.Send(
            new ProposeSurveyAppointmentCommand(
                orderId,
                SurveyAppointmentPurpose.Capture,
                proposedStartAt,
                proposedEndAt,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedCurrentAppointmentId,
                request.ExpectedCurrentAppointmentVersion,
                ContractScope.V3),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                SurveyAppointmentResponseMapper.ToResponse(response),
                statusCode: StatusCodes.Status201Created));
    }

    [HttpPost("{orderId:guid}/capture-attempts")]
    public async Task<IResult> PrepareCaptureAttempt(
        [FromRoute] Guid orderId,
        [FromBody] PrepareV3CaptureAttemptRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.PrepareAttemptAsync(
            new PrepareCaptureAttemptRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                actorId,
                request.DroneId,
                request.Reason,
                executionContext.CorrelationId,
                request.OperationId!.Value,
                request.PayloadFingerprint),
            cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status201Created, result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{orderId:guid}/capture-appointments/no-show")]
    public async Task<IResult> RecordCaptureNoShow(
        [FromRoute] Guid orderId,
        [FromBody] RecordV3CaptureNoShowRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.RecordNoShowAsync(
            new RecordCaptureNoShowRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                request.AppointmentId!.Value,
                request.ExpectedAppointmentVersion!.Value,
                actorId,
                request.OccurredAt!.Value,
                request.Reason,
                request.EvidenceReference,
                executionContext.CorrelationId,
                request.OperationId!.Value,
                request.PayloadFingerprint),
            cancellationToken);
        return CoordinatorResult(
            result.Success,
            result,
            StatusCodes.Status201Created,
            result.ErrorCode,
            result.ErrorMessage);
    }

    [HttpPost("{orderId:guid}/disease-capture-attempts")]
    public async Task<IResult> ScheduleDiseaseCapture(
        [FromRoute] Guid orderId,
        [FromBody] ScheduleV3DiseaseCaptureRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.ScheduleDiseaseCaptureAsync(
            new ScheduleDiseaseCaptureRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                actorId,
                request.DroneId!.Value,
                request.StartAt!.Value.ToUniversalTime(),
                request.EndAt!.Value.ToUniversalTime(),
                executionContext.CorrelationId,
                request.OperationId!.Value),
            cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status201Created, result.ErrorCode, result.ErrorMessage);
    }

    [HttpPut("{orderId:guid}/disease-capture-attempts/{attemptId:guid}/appointment")]
    public async Task<IResult> RescheduleDiseaseCapture(
        [FromRoute] Guid orderId,
        [FromRoute] Guid attemptId,
        [FromBody] RescheduleV3DiseaseCaptureRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.RescheduleDiseaseCaptureAsync(
            new RescheduleDiseaseCaptureRequest(
                orderId, attemptId, request.ExpectedOrderVersion!.Value, actorId, request.DroneId!.Value,
                request.StartAt!.Value.ToUniversalTime(), request.EndAt!.Value.ToUniversalTime(),
                executionContext.CorrelationId),
            cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status200OK, result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{orderId:guid}/capture-attempts/{attemptId:guid}/start")]
    public async Task<IResult> StartCaptureFlight(
        [FromRoute] Guid orderId,
        [FromRoute] Guid attemptId,
        [FromBody] StartV3CaptureFlightRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.StartFlightAsync(
            new StartCaptureFlightRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                attemptId,
                request.ExpectedAttemptVersion!.Value,
                request.MissionId!.Value,
                actorId,
                executionContext.CorrelationId,
                request.OperationId!.Value,
                request.PayloadFingerprint), cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status200OK, result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{orderId:guid}/capture-attempts/{attemptId:guid}/complete-flight")]
    public async Task<IResult> CompleteCaptureFlight(
        [FromRoute] Guid orderId,
        [FromRoute] Guid attemptId,
        [FromBody] EndV3CaptureFlightRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.EndFlightAsync(
            new EndCaptureFlightRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                attemptId,
                request.ExpectedAttemptVersion!.Value,
                actorId,
                executionContext.CorrelationId,
                request.EndedAt!.Value,
                request.FlightCompletedReceipt,
                request.OperationId!.Value,
                request.PayloadFingerprint), cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status200OK, result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{orderId:guid}/capture-attempts/{attemptId:guid}/fail-flight")]
    public async Task<IResult> FailCaptureFlight(
        [FromRoute] Guid orderId,
        [FromRoute] Guid attemptId,
        [FromBody] FailV3CaptureFlightRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.RecordFlightFailureAsync(
            new RecordFlightFailureRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                attemptId,
                request.ExpectedAttemptVersion!.Value,
                actorId,
                executionContext.CorrelationId,
                request.FailedAt!.Value,
                request.FailureReason,
                request.FailureIncidentEvidence,
                request.OperationId!.Value,
                request.PayloadFingerprint), cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status200OK, result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{orderId:guid}/capture-attempts/{attemptId:guid}/input-manifest/freeze")]
    [ProducesResponseType<FreezeCaptureInputManifestResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> FreezeCaptureManifest(
        [FromRoute] Guid orderId,
        [FromRoute] Guid attemptId,
        [FromBody] FreezeV3CaptureManifestRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.FreezeInputManifestAsync(
            new FreezeCaptureInputManifestRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                attemptId,
                request.ExpectedAttemptVersion!.Value,
                request.MissionId!.Value,
                actorId,
                executionContext.CorrelationId,
                request.OperationId!.Value,
                request.PayloadFingerprint), cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status201Created, result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{orderId:guid}/capture-attempts/{attemptId:guid}/input-manifest/reopen")]
    [ProducesResponseType<ReopenCaptureInputManifestResult>(StatusCodes.Status200OK)]
    public async Task<IResult> ReopenCaptureManifest(
        [FromRoute] Guid orderId,
        [FromRoute] Guid attemptId,
        [FromBody] ReopenV3CaptureManifestRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.ReopenInputManifestAsync(
            new ReopenCaptureInputManifestRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                attemptId,
                request.ExpectedAttemptVersion!.Value,
                request.ManifestId!.Value,
                actorId,
                executionContext.CorrelationId,
                request.OperationId!.Value,
                request.PayloadFingerprint,
                request.Reason), cancellationToken);
        if (result.ErrorCode == "MANAGER_FORBIDDEN")
            return Results.Forbid();
        if (result.ErrorCode == "CAPTURE_MANIFEST_NOT_FOUND")
            return Results.NotFound();
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status200OK, result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("{orderId:guid}/capture-attempts/{attemptId:guid}/input-approval")]
    public async Task<IResult> ApproveCaptureInput(
        [FromRoute] Guid orderId,
        [FromRoute] Guid attemptId,
        [FromBody] ApproveV3CaptureInputRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.ApproveInputAsync(
            new ApproveCaptureInputRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                attemptId,
                request.ExpectedAttemptVersion!.Value,
                request.ManifestId!.Value,
                request.ManifestFingerprint,
                request.ProcessingJobId!.Value,
                actorId,
                executionContext.CorrelationId,
                request.OperationId!.Value,
                request.PayloadFingerprint,
                request.Notes), cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status201Created, result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>Đồng bộ trạng thái nhiệm vụ V3 theo bản duyệt đầu vào hiện hành.</summary>
    [HttpPost("{orderId:guid}/capture-attempts/{attemptId:guid}/input-approval/recover")]
    [ProducesResponseType<RecoverCaptureMissionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> RecoverApprovedMission(
        [FromRoute] Guid orderId,
        [FromRoute] Guid attemptId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        var result = await captureCoordinator.RecoverApprovedMissionAsync(
            new RecoverCaptureMissionRequest(orderId, attemptId, actorId, executionContext.CorrelationId),
            cancellationToken);

        if (result.ErrorCode == "MANAGER_FORBIDDEN")
            return Results.Forbid();
        if (result.ErrorCode == "CAPTURE_NOT_FOUND")
            return Results.NotFound();

        return CoordinatorResult(result.Success, result,
            StatusCodes.Status200OK, result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>Đọc danh sách các lần bay (CaptureAttempt), mission gắn kèm và lịch sử chuyển trạng thái cho đơn V3.</summary>
    [HttpGet("{orderId:guid}/capture-attempts")]
    [ProducesResponseType<IReadOnlyList<V3CaptureAttemptApiResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> GetCaptureAttempts(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetManagerSurveyOrderCaptureAttemptsQuery(orderId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(V3SurveyResultAndInvoiceMapper.ToResponse(response)));
    }

    [HttpPost("{orderId:guid}/capture-attempts/{parentAttemptId:guid}/quality-reflights")]
    public async Task<IResult> CreateQualityReflight(
        [FromRoute] Guid orderId,
        [FromRoute] Guid parentAttemptId,
        [FromBody] CreateV3QualityReflightRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Results.Forbid();
        DateTimeOffset proposedStartAt = default;
        DateTimeOffset proposedEndAt = default;
        string? errorMessage = null;
        if (request.ScheduledDate is not DateOnly scheduledDate ||
            !SurveyAppointmentWindow.TryResolveForDate(
                scheduledDate,
                timeProvider.GetUtcNow(),
                out proposedStartAt,
                out proposedEndAt,
                out errorMessage))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ScheduledDate)] =
                    [errorMessage ?? "A valid future scheduled date is required."]
            });
        }

        var result = await captureCoordinator.CreateQualityReflightAsync(
            new CreateQualityReflightRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                parentAttemptId,
                request.ExpectedParentAttemptVersion!.Value,
                request.DroneId!.Value,
                proposedStartAt,
                proposedEndAt,
                actorId,
                executionContext.CorrelationId,
                request.Reason,
                request.TechnicalQualityEvidence,
                request.OperationId!.Value,
                request.PayloadFingerprint),
            cancellationToken);
        return CoordinatorResult(result.Success, result,
            StatusCodes.Status201Created, result.ErrorCode, result.ErrorMessage);
    }

    /// <summary>Đọc trạng thái review kết quả và các token hiện hành để freeze hoặc finalize hóa đơn V3.</summary>
    [HttpGet("{orderId:guid}/result-review")]
    [ProducesResponseType<V3SurveyResultReviewApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> GetResultReview(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyResultReviewQuery(orderId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(V3SurveyResultAndInvoiceMapper.ToResponse(response)));
    }

    /// <summary>Ghi nhận một revision review COMBO bất biến theo exact batch/previous tokens.</summary>
    [HttpPost("{orderId:guid}/result-review/combo-reviews")]
    [ProducesResponseType<V3ComboReviewApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<V3ComboReviewApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> RecordComboReview(
        [FromRoute] Guid orderId,
        [FromBody] RecordV3ComboReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            V3SurveyResultAndInvoiceMapper.ToCommand(orderId, request),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                V3SurveyResultAndInvoiceMapper.ToResponse(response),
                statusCode: response.IsReplay
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status201Created));
    }

    /// <summary>Chốt cứng bằng chứng kết quả nội bộ (MAP hoặc COMBO) và chấp nhận capture attempt.</summary>
    [HttpPost("{orderId:guid}/result-review/freeze")]
    [ProducesResponseType<V3FreezeInternalResultApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<V3FreezeInternalResultApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> FreezeInternalResult(
        [FromRoute] Guid orderId,
        [FromBody] FreezeV3InternalResultRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            V3SurveyResultAndInvoiceMapper.ToCommand(orderId, request),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                V3SurveyResultAndInvoiceMapper.ToResponse(response),
                statusCode: response.IsReplay
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status201Created));
    }

    /// <summary>Đọc hóa đơn V3 hiện hành cùng bộ token phiên bản để chuẩn bị correction hoặc bàn giao settlement.</summary>
    [HttpGet("{orderId:guid}/invoice")]
    [ProducesResponseType<V3ManagerInvoiceApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> GetInvoice(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetManagerSurveyOrderInvoiceQuery(orderId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(V3SurveyResultAndInvoiceMapper.ToResponse(response)));
    }

    /// <summary>Chốt hóa đơn V3 lần đầu từ approval và count snapshot đã đóng băng.</summary>
    [HttpPost("{orderId:guid}/invoice/finalize")]
    [ProducesResponseType<V3FinalizeInvoiceApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<V3FinalizeInvoiceApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> FinalizeInvoice(
        [FromRoute] Guid orderId,
        [FromBody] FinalizeV3InvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            V3SurveyResultAndInvoiceMapper.ToCommand(orderId, request),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                V3SurveyResultAndInvoiceMapper.ToResponse(response),
                statusCode: response.IsReplay
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status201Created));
    }

    /// <summary>Hiệu chỉnh số trụ và cập nhật nguyên tử revision count/approval/invoice (và COMBO review nếu cần) trước phát hành.</summary>
    [HttpPost("{orderId:guid}/invoice/corrections")]
    [ProducesResponseType<V3CorrectPreReleaseInvoiceApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<V3CorrectPreReleaseInvoiceApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> CorrectPreReleaseInvoice(
        [FromRoute] Guid orderId,
        [FromBody] CorrectV3PreReleaseInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            V3SurveyResultAndInvoiceMapper.ToCommand(orderId, request),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                V3SurveyResultAndInvoiceMapper.ToResponse(response),
                statusCode: response.IsReplay
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status201Created));
    }

    private bool TryGetActor(out Guid actorId)
    {
        actorId = executionContext.ActorId ?? Guid.Empty;
        return executionContext.IsInitialized &&
            actorId != Guid.Empty &&
            executionContext.CorrelationId != Guid.Empty;
    }

    private static IResult CoordinatorResult<T>(
        bool success,
        T response,
        int successStatus,
        string? errorCode,
        string? errorMessage) => success
        ? Results.Json(response, statusCode: successStatus)
        : Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: errorCode ?? "V3 capture conflict",
            detail: errorMessage);
}
