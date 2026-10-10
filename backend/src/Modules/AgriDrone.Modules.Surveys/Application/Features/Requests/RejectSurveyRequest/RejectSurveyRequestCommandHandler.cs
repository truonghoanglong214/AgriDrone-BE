using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.RejectSurveyRequest;

internal sealed class RejectSurveyRequestCommandHandler(
    ISurveyRequestRepository requestRepository,
    ISurveyIntegrationOutbox integrationOutbox,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        RejectSurveyRequestCommand,
        Result<RejectSurveyRequestResponse>>
{
    public async Task<Result<RejectSurveyRequestResponse>> Handle(
        RejectSurveyRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminContext(out var actorId))
        {
            return Result.Failure<RejectSurveyRequestResponse>(
                SurveyRequestError.CurrentSystemAdminRequired());
        }

        var surveyRequest = await requestRepository.GetByIdAsync(
            request.SurveyRequestId,
            cancellationToken);
        if (surveyRequest is null)
        {
            return Result.Failure<RejectSurveyRequestResponse>(
                SurveyRequestError.NotFound(request.SurveyRequestId));
        }

        using var checklistSnapshot = CreateChecklistSnapshot(
            request.Checklist);
        using var oldData = SurveyRequestReviewAuditSnapshot.Create(
            surveyRequest);
        var now = timeProvider.GetUtcNow();
        SurveyRequestReview review;
        try
        {
            surveyRequest.EnsureVersion(request.ExpectedVersion);
            review = surveyRequest.Reject(
                checklistSnapshot,
                request.Reason,
                actorId,
                now);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<RejectSurveyRequestResponse>(
                MapDomainError(exception, surveyRequest.Id));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<RejectSurveyRequestResponse>(
                SurveyRequestError.InvalidChecklist(exception.Message));
        }

        using var newData = SurveyRequestReviewAuditSnapshot.Create(
            surveyRequest,
            review.Id,
            request.Checklist.Version);

        var messageId = Guid.CreateVersion7();
        var rejectionNotification = new EmailNotificationRequestedV1(
            NotificationId: messageId,
            TemplateKey: EmailTemplateKeys.SurveyRequestRejected,
            Recipients:
            [
                new EmailRecipientV1(
                    surveyRequest.ApplicantEmail,
                    surveyRequest.ApplicantName)
            ],
            Variables: new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.ApplicantName] =
                    surveyRequest.ApplicantName,
                [EmailTemplateVariableKeys.RequestNumber] =
                    surveyRequest.RequestNumber,
                [EmailTemplateVariableKeys.SurveyServiceName] =
                    surveyRequest.SurveyService.Name,
                [EmailTemplateVariableKeys.RejectionReason] = review.Reason
            });
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.EmailNotificationRequestedV1,
            messageId,
            executionContext.CorrelationId,
            surveyRequest.TenantId,
            actorId,
            now,
            rejectionNotification);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "SurveyRequest",
                        surveyRequest.Id,
                        "REJECT",
                        oldData,
                        newData,
                        now);
                    integrationOutbox.Add(
                        envelope,
                        surveyRequest.Id.ToString("D"));
                    await unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);
                    return true;
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<RejectSurveyRequestResponse>(
                SurveyRequestError.VersionConflict());
        }

        return Result.Success(new RejectSurveyRequestResponse(
            surveyRequest.Id,
            surveyRequest.RequestNumber,
            surveyRequest.Status,
            surveyRequest.Version,
            review.Id,
            review.ReviewedAt));
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
                SurveyRequestError.VersionConflict(),
            SurveyRequestDomainErrorCodes.InvalidTransition =>
                SurveyRequestError.NotReviewable(requestId),
            _ => SurveyRequestError.InvalidTransition(exception.Message)
        };
}
