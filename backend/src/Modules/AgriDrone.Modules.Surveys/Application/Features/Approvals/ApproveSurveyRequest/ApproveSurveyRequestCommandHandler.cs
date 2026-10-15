using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal sealed class ApproveSurveyRequestCommandHandler(
    ISurveyApprovalUnitOfWork unitOfWork,
    ISurveyApprovalRequestRepository requestRepository,
    ISurveyApprovalIdempotencyRepository idempotencyRepository,
    ISurveyApprovalCaptureAuthorizationRepository authorizationRepository,
    ISurveyApprovalPrerequisiteResolver prerequisiteResolver,
    IEnumerable<ISurveyRequestKindApprovalOrchestrator> orchestrators,
    ISurveyApprovalOutbox approvalOutbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        ApproveSurveyRequestCommand,
        Result<ApproveSurveyRequestResponse>>
{
    private readonly Dictionary<
        SurveyRequestKind,
        ISurveyRequestKindApprovalOrchestrator> _orchestrators =
        orchestrators.ToDictionary(orchestrator => orchestrator.RequestKind);

    public async Task<Result<ApproveSurveyRequestResponse>> Handle(
        ApproveSurveyRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminContext(out var actorId))
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.CurrentSystemAdminRequired());
        }

        if (!IsCompleteApprovalChecklist(command.Checklist))
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.InvalidChecklist(
                    "Every approval checklist item must be confirmed."));
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) ||
            command.IdempotencyKey.Trim().Length >
                SurveyApprovalIdempotencyRecord.MaximumKeyLength)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.InvalidIdempotencyKey());
        }

        var idempotencyKey = command.IdempotencyKey.Trim();
        var commandFingerprint = SurveyApprovalCommandFingerprint.Create(
            command);
        var approvedAt = timeProvider.GetUtcNow();
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                transactionCancellationToken => ApproveWithinTransactionAsync(
                    command,
                    actorId,
                    idempotencyKey,
                    commandFingerprint,
                    approvedAt,
                    transactionCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ResolveAfterConcurrentFailureAsync(
                command,
                actorId,
                idempotencyKey,
                commandFingerprint,
                SurveyApprovalError.VersionConflict(),
                cancellationToken);
        }
        catch (PostgresException exception)
            when (exception.SqlState is
                PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected)
        {
            return await ResolveAfterConcurrentFailureAsync(
                command,
                actorId,
                idempotencyKey,
                commandFingerprint,
                SurveyApprovalError.ConcurrentApproval(),
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var error = exception.HasPostgresSqlState(
                    PostgresErrorCodes.SerializationFailure) ||
                exception.HasPostgresSqlState(
                    PostgresErrorCodes.DeadlockDetected)
                ? SurveyApprovalError.ConcurrentApproval()
                : MapPersistenceError(exception);
            if (error is null)
            {
                throw;
            }

            return await ResolveAfterConcurrentFailureAsync(
                command,
                actorId,
                idempotencyKey,
                commandFingerprint,
                error,
                cancellationToken);
        }
    }

    private async Task<Result<ApproveSurveyRequestResponse>>
        ApproveWithinTransactionAsync(
            ApproveSurveyRequestCommand command,
            Guid actorId,
            string idempotencyKey,
            string commandFingerprint,
            DateTimeOffset approvedAt,
            CancellationToken cancellationToken)
    {
        var surveyRequest = await requestRepository.GetForApprovalAsync(
            command.SurveyRequestId,
            cancellationToken);
        if (surveyRequest is null)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.RequestNotFound(command.SurveyRequestId));
        }

        var existingResult = await ResolveExistingOperationAsync(
            command.SurveyRequestId,
            actorId,
            idempotencyKey,
            commandFingerprint,
            cancellationToken);
        if (existingResult is not null)
        {
            return existingResult;
        }

        if (surveyRequest.Status == SurveyRequestStatus.Approved)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.ConcurrentApproval());
        }

        if (!SurveyServiceMvpPolicy.IsSellable(
                surveyRequest.SurveyService.ServiceType))
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.RequestContextInvalid());
        }

        try
        {
            surveyRequest.EnsureVersion(command.ExpectedVersion);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                MapDomainError(exception, surveyRequest.Id));
        }

        var idempotencyClaim = SurveyApprovalIdempotencyRecord.Claim(
            surveyRequest.Id,
            actorId,
            idempotencyKey,
            commandFingerprint,
            approvedAt);
        await idempotencyRepository.AddClaimAsync(
            idempotencyClaim,
            cancellationToken);

        var prerequisiteResult = await prerequisiteResolver.ResolveAsync(
            surveyRequest,
            command.SelectedSystemManagerProfileId,
            approvedAt,
            cancellationToken);
        if (prerequisiteResult.IsFailure)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                prerequisiteResult.Error);
        }

        if (!_orchestrators.TryGetValue(
                surveyRequest.Kind,
                out var orchestrator))
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.RequestContextInvalid());
        }

        var orchestrationResult = await orchestrator.OrchestrateAsync(
            surveyRequest,
            prerequisiteResult.Value,
            new SurveyApprovalExecutionContext(actorId, approvedAt),
            cancellationToken);
        if (orchestrationResult.IsFailure)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                orchestrationResult.Error);
        }

        using var oldData = SurveyApprovalAuditSnapshot.CreateBefore(
            surveyRequest);
        using var checklistSnapshot = CreateChecklistSnapshot(
            command.Checklist);

        SurveyRequestReview review;
        try
        {
            review = surveyRequest.Approve(
                checklistSnapshot,
                command.Reason,
                actorId,
                approvedAt);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                MapDomainError(exception, surveyRequest.Id));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.InvalidChecklist(exception.Message));
        }

        var approval = orchestrationResult.Value;
        authorizationRepository.Add(
            SurveyCaptureAuthorization.CreatePending(
                approval.Order.Id,
                SurveyCaptureAuthorizationType.WrittenDataUseConsent,
                approvedAt));
        authorizationRepository.Add(
            SurveyCaptureAuthorization.CreatePending(
                approval.Order.Id,
                SurveyCaptureAuthorizationType.FlightPermission,
                approvedAt));
        using var newData = SurveyApprovalAuditSnapshot.CreateAfter(
            surveyRequest,
            review,
            approval,
            command.Checklist.Version);

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            "SurveyRequest",
            surveyRequest.Id,
            "APPROVE",
            oldData,
            newData,
            approvedAt);

        var eventMessageId = Guid.CreateVersion7();
        var approvalEvent = new SurveyRequestApprovedV1(
            review.Id,
            surveyRequest.Id,
            surveyRequest.Kind.ToString(),
            approval.Order.Id,
            approval.TenantId,
            approval.FarmId,
            surveyRequest.SurveyServiceId,
            approval.SystemManagerProfileId,
            approval.PrimaryManagerAssignmentId,
            approval.Order.RequiresBaselineMapping,
            approval.Order.PreviousCompatibleOrderId,
            approvedAt);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyRequestApprovedV1,
            eventMessageId,
            executionContext.CorrelationId,
            approval.TenantId,
            actorId,
            approvedAt,
            approvalEvent);
        approvalOutbox.Add(envelope, surveyRequest.Id.ToString("D"));

        var response = new ApproveSurveyRequestResponse(
            surveyRequest.Id,
            surveyRequest.RequestNumber,
            surveyRequest.Status,
            surveyRequest.Version,
            review.Id,
            approval.Order.Id,
            approval.Order.OrderNumber,
            approval.Order.Status,
            approval.TenantId,
            approval.FarmId,
            approval.SystemManagerProfileId,
            approval.PrimaryManagerAssignmentId,
            approval.OwnerInvitationId,
            approval.Order.RequiresBaselineMapping,
            approval.Order.PreviousCompatibleOrderId,
            approvedAt);
        using var resultSnapshot = SurveyApprovalReplaySnapshot.Create(response);
        idempotencyClaim.Complete(resultSnapshot, approvedAt);

        return Result.Success(response);
    }

    private Task<Result<ApproveSurveyRequestResponse>>
        ResolveAfterConcurrentFailureAsync(
            ApproveSurveyRequestCommand command,
            Guid actorId,
            string idempotencyKey,
            string commandFingerprint,
            AppError fallbackError,
            CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                var existingResult = await ResolveExistingOperationAsync(
                    command.SurveyRequestId,
                    actorId,
                    idempotencyKey,
                    commandFingerprint,
                    transactionCancellationToken);
                return existingResult ??
                    Result.Failure<ApproveSurveyRequestResponse>(fallbackError);
            },
            cancellationToken);

    private async Task<Result<ApproveSurveyRequestResponse>?>
        ResolveExistingOperationAsync(
            Guid surveyRequestId,
            Guid actorId,
            string idempotencyKey,
            string commandFingerprint,
            CancellationToken cancellationToken)
    {
        var operationByKey = await idempotencyRepository
            .GetByActorAndKeyAsync(
                actorId,
                idempotencyKey,
                cancellationToken);
        if (operationByKey is not null)
        {
            return operationByKey.Matches(
                    surveyRequestId,
                    actorId,
                    idempotencyKey,
                    commandFingerprint)
                ? SurveyApprovalReplaySnapshot.Restore(operationByKey)
                : Result.Failure<ApproveSurveyRequestResponse>(
                    SurveyApprovalError.IdempotencyConflict());
        }

        var operationByRequest = await idempotencyRepository
            .GetByRequestIdAsync(surveyRequestId, cancellationToken);
        return operationByRequest is null
            ? null
            : Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.ConcurrentApproval());
    }

    private bool TryGetAdminContext(out Guid actorId)
    {
        if (executionContext.IsInitialized &&
            executionContext.ActorId is Guid currentActorId &&
            executionContext.CorrelationId != Guid.Empty)
        {
            actorId = currentActorId;
            return true;
        }

        actorId = Guid.Empty;
        return false;
    }

    private static bool IsCompleteApprovalChecklist(
        SurveyRequestReviewChecklistInput checklist) =>
        checklist is not null &&
        checklist.Version == SurveyRequestReviewChecklistDefinition.Version &&
        checklist.ContactValid &&
        checklist.IsDragonFruitFarm &&
        checklist.IsServiceAreaSupported &&
        checklist.IsLocationSufficient &&
        checklist.IsPreliminaryLegalAndFlightFeasible &&
        checklist.IsEligibleSystemManagerAvailable &&
        checklist.IsWrittenConsentFeasible &&
        checklist.IsFlightPermissionFeasible;

    private static JsonDocument CreateChecklistSnapshot(
        SurveyRequestReviewChecklistInput checklist) =>
        JsonSerializer.SerializeToDocument(new
        {
            version = checklist.Version,
            contactValid = checklist.ContactValid,
            isDragonFruitFarm = checklist.IsDragonFruitFarm,
            isServiceAreaSupported = checklist.IsServiceAreaSupported,
            isLocationSufficient = checklist.IsLocationSufficient,
            isPreliminaryLegalAndFlightFeasible =
                checklist.IsPreliminaryLegalAndFlightFeasible,
            isEligibleSystemManagerAvailable =
                checklist.IsEligibleSystemManagerAvailable,
            isWrittenConsentFeasible = checklist.IsWrittenConsentFeasible,
            isFlightPermissionFeasible = checklist.IsFlightPermissionFeasible,
            notes = string.IsNullOrWhiteSpace(checklist.Notes)
                ? null
                : checklist.Notes.Trim()
        });

    private static AppError MapDomainError(
        SurveyDomainException exception,
        Guid requestId) =>
        exception.Code switch
        {
            SurveyRequestDomainErrorCodes.VersionConflict =>
                SurveyApprovalError.VersionConflict(),
            SurveyRequestDomainErrorCodes.InvalidTransition =>
                SurveyApprovalError.RequestNotUnderReview(requestId),
            _ => AppError.Failure(
                "SurveyApproval.InvalidTransition",
                exception.Message)
        };

    private static AppError? MapPersistenceError(
        DbUpdateException exception)
    {
        if (exception.IsUniqueConstraintViolation(
                SurveyApprovalPersistenceConstraints.IdempotencyRequest,
                SurveyApprovalPersistenceConstraints.IdempotencyActorKey,
                SurveyApprovalPersistenceConstraints.RequestOrder))
        {
            return SurveyApprovalError.ConcurrentApproval();
        }

        if (exception.IsUniqueConstraintViolation(
                SurveyApprovalPersistenceConstraints.OrderNumber))
        {
            return SurveyApprovalError.OrderNumberConflict();
        }

        if (exception.IsUniqueConstraintViolation(
                SurveyApprovalPersistenceConstraints.ActiveTenantCode))
        {
            return SurveyApprovalError.TenantProvisioningConflict();
        }

        if (exception.IsUniqueConstraintViolation(
                SurveyApprovalPersistenceConstraints.ActiveFarmCode))
        {
            return SurveyApprovalError.FarmProvisioningConflict();
        }

        if (exception.IsUniqueConstraintViolation(
                SurveyApprovalPersistenceConstraints.ActiveFarmAssignment))
        {
            return SurveyApprovalError.PrimaryAssignmentConflict();
        }

        return exception.IsUniqueConstraintViolation(
                SurveyApprovalPersistenceConstraints.PendingOwnerInvitation)
            ? SurveyApprovalError.OwnerInvitationConflict()
            : null;
    }
}
