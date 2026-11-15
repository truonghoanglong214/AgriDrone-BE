using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Api.Mapping;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;
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
