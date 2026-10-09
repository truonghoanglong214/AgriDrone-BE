using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Abstractions.Requests;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequest;

internal sealed class SubmitNewCustomerSurveyRequestCommandHandler(
    ISurveyRequestRepository requestRepository,
    ISurveyServiceRepository serviceRepository,
    ISurveyCatalogueQueries catalogueQueries,
    ISurveyRequestIdempotencyResolver idempotencyResolver,
    ISurveyRequestNumberGenerator requestNumberGenerator,
    ISurveyIntegrationOutbox integrationOutbox,
    ISurveysUnitOfWork unitOfWork,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        SubmitNewCustomerSurveyRequestCommand,
        Result<SubmitNewCustomerSurveyRequestResponse>>
{
    public async Task<Result<SubmitNewCustomerSurveyRequestResponse>> Handle(
        SubmitNewCustomerSurveyRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestResponse>(
                SurveyRequestError.RequestContextRequired());
        }

        SurveyRequestSubmissionSnapshot submission;
        try
        {
            submission = SurveyRequestSubmissionSnapshot.Create(
                SurveyRequestKind.NewCustomer,
                tenantId: null,
                farmId: null,
                requestedByUserId: null,
                request.SurveyServiceId,
                request.ApplicantName,
                request.ApplicantEmail,
                request.ApplicantPhone,
                request.FarmName,
                request.FarmAddress,
                request.ApproximateAreaHa,
                request.Longitude,
                request.Latitude,
                mapSrid: 4326,
                request.EstimatedPoleCount,
                request.PreferredStartAt,
                request.PreferredEndAt,
                request.Notes);
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestResponse>(
                SurveyRequestError.InvalidInput(
                    "The survey request payload is invalid."));
        }

        var idempotency = await idempotencyResolver.ResolveAsync(
            request.Idempotency,
            submission,
            cancellationToken);
        var previousResult = ResolvePrevious(idempotency);
        if (previousResult is not null)
        {
            return previousResult;
        }

        var service = await serviceRepository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);
        if (service is null ||
            service.Status is not SurveyServiceStatus.Active and
                not SurveyServiceStatus.Experimental)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestResponse>(
                SurveyRequestError.ServiceUnavailable());
        }

        var now = timeProvider.GetUtcNow();
        var effectivePrice = await catalogueQueries
            .GetEffectivePerPolePriceAsync(
                service.Id,
                now,
                cancellationToken);
        if (effectivePrice is null)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestResponse>(
                SurveyRequestError.ServiceUnavailable());
        }

        SurveyRequest surveyRequest;
        try
        {
            surveyRequest = SurveyRequest.CreateNewCustomer(
                requestNumberGenerator.Create(),
                service,
                request.Idempotency,
                submission.ApplicantName,
                submission.ApplicantEmail,
                submission.ApplicantPhone,
                submission.FarmName,
                submission.FarmAddress,
                submission.ApproximateAreaHa,
                new Point(submission.Longitude, submission.Latitude)
                {
                    SRID = submission.MapSrid
                },
                submission.EstimatedPoleCount,
                submission.PreferredStartAt,
                submission.PreferredEndAt,
                submission.Notes,
                now);
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestResponse>(
                SurveyRequestError.InvalidInput(
                    "The survey request payload is invalid."));
        }

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            surveyRequest.RequestNumber,
            surveyRequest.Kind,
            surveyRequest.Status,
            surveyRequest.SurveyServiceId
        });

        var messageId = Guid.CreateVersion7();
        var acknowledgement = new EmailNotificationRequestedV1(
            NotificationId: messageId,
            TemplateKey: EmailTemplateKeys.SurveyRequestAcknowledgement,
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
                [EmailTemplateVariableKeys.SurveyServiceName] = service.Name
            });
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.EmailNotificationRequestedV1,
            messageId,
            executionContext.CorrelationId,
            tenantId: null,
            actorId: null,
            now,
            acknowledgement);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    requestRepository.Add(surveyRequest);
                    unitOfWork.AddAuditLog(AuditLog.ForSystemAction(
                        executionContext.CorrelationId,
                        "SurveyRequest",
                        surveyRequest.Id,
                        "PUBLIC_SUBMIT",
                        oldData: null,
                        auditData,
                        now));
                    integrationOutbox.Add(
                        envelope,
                        surveyRequest.Id.ToString("D"));

                    await unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);
                    return true;
                },
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                SurveyRequestPersistenceConstraints.CallerIdempotency))
        {
            var raceResolution = await idempotencyResolver.ResolveAsync(
                request.Idempotency,
                submission,
                cancellationToken);
            return ResolvePrevious(raceResolution) ??
                Result.Failure<SubmitNewCustomerSurveyRequestResponse>(
                    SurveyRequestError.Duplicate());
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                SurveyRequestPersistenceConstraints.RequestNumber))
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestResponse>(
                SurveyRequestError.RequestNumberConflict());
        }

        return Result.Success(MapResponse(surveyRequest));
    }

    private static Result<SubmitNewCustomerSurveyRequestResponse>?
        ResolvePrevious(SurveyRequestIdempotencyResolution resolution) =>
        resolution.Outcome switch
        {
            SurveyRequestIdempotencyOutcome.NewSubmission => null,
            SurveyRequestIdempotencyOutcome.Replay =>
                Result.Success(MapResponse(resolution.ExistingResponse!)),
            SurveyRequestIdempotencyOutcome.PayloadMismatch =>
                Result.Failure<SubmitNewCustomerSurveyRequestResponse>(
                    SurveyRequestError.IdempotencyPayloadMismatch()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution.Outcome,
                "Unsupported idempotency outcome.")
        };

    private static SubmitNewCustomerSurveyRequestResponse MapResponse(
        SurveyRequest request) =>
        new(
            request.Id,
            request.RequestNumber,
            request.Kind,
            request.Status,
            request.CreatedAt);

    private static SubmitNewCustomerSurveyRequestResponse MapResponse(
        SurveyRequestAcknowledgementResponse response) =>
        new(
            response.Id,
            response.RequestNumber,
            response.Kind,
            response.Status,
            response.CreatedAt);
}
