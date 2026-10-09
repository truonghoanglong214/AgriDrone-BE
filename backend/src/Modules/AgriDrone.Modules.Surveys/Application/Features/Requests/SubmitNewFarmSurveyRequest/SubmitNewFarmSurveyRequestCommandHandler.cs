using System.Net.Mail;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Identity;
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

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewFarmSurveyRequest;

internal sealed class SubmitNewFarmSurveyRequestCommandHandler(
    ITenantOwnerRequestReferenceQuery tenantOwnerReferenceQuery,
    ISurveyRequestRepository requestRepository,
    ISurveyServiceRepository serviceRepository,
    ISurveyCatalogueQueries catalogueQueries,
    ISurveyRequestIdempotencyResolver idempotencyResolver,
    ISurveyRequestNumberGenerator requestNumberGenerator,
    ISurveyIntegrationOutbox integrationOutbox,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        SubmitNewFarmSurveyRequestCommand,
        Result<SubmitNewFarmSurveyRequestResponse>>
{
    public async Task<Result<SubmitNewFarmSurveyRequestResponse>> Handle(
        SubmitNewFarmSurveyRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.TenantId is not Guid tenantId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                SurveyRequestError.CurrentTenantOwnerRequired());
        }

        var owner = await tenantOwnerReferenceQuery.GetActiveOwnerAsync(
            tenantId,
            actorId,
            cancellationToken);
        if (owner is null)
        {
            return Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                SurveyRequestError.Forbidden());
        }

        if (!HasCompleteApplicantProfile(owner))
        {
            return Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                SurveyRequestError.ApplicantProfileIncomplete());
        }

        RequestIdempotency idempotency;
        SurveyRequestSubmissionSnapshot submission;
        try
        {
            idempotency = SurveyRequestIdempotencyFactory.ForTenantActor(
                tenantId,
                actorId,
                request.IdempotencyKey);
            submission = SurveyRequestSubmissionSnapshot.Create(
                SurveyRequestKind.ExistingTenantNewFarm,
                tenantId,
                farmId: null,
                requestedByUserId: actorId,
                request.SurveyServiceId,
                owner.FullName,
                owner.Email,
                owner.Phone!,
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
            return Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                SurveyRequestError.InvalidInput(
                    "The survey request payload is invalid."));
        }

        var idempotencyResolution = await idempotencyResolver.ResolveAsync(
            idempotency,
            submission,
            cancellationToken);
        var previousResult = ResolvePrevious(idempotencyResolution);
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
            return Result.Failure<SubmitNewFarmSurveyRequestResponse>(
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
            return Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                SurveyRequestError.ServiceUnavailable());
        }

        SurveyRequest surveyRequest;
        try
        {
            surveyRequest = SurveyRequest.CreateExistingTenantNewFarm(
                tenantId,
                actorId,
                requestNumberGenerator.Create(),
                service,
                idempotency,
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
            return Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                SurveyRequestError.InvalidInput(
                    "The survey request payload is invalid."));
        }

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            surveyRequest.RequestNumber,
            surveyRequest.Kind,
            surveyRequest.Status,
            surveyRequest.SurveyServiceId,
            surveyRequest.TenantId,
            surveyRequest.RequestedByUserId
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
            tenantId,
            actorId,
            now,
            acknowledgement);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    requestRepository.Add(surveyRequest);
                    auditWriter.AddUserAction(
                        unitOfWork,
                        tenantId,
                        farmId: null,
                        actorId,
                        executionContext.CorrelationId,
                        "SurveyRequest",
                        surveyRequest.Id,
                        "TENANT_OWNER_NEW_FARM_SUBMIT",
                        oldData: null,
                        auditData,
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
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                SurveyRequestPersistenceConstraints.CallerIdempotency))
        {
            var raceResolution = await idempotencyResolver.ResolveAsync(
                idempotency,
                submission,
                cancellationToken);
            return ResolvePrevious(raceResolution) ??
                Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                    SurveyRequestError.Duplicate());
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                SurveyRequestPersistenceConstraints.RequestNumber))
        {
            return Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                SurveyRequestError.RequestNumberConflict());
        }

        return Result.Success(MapResponse(surveyRequest));
    }

    private static Result<SubmitNewFarmSurveyRequestResponse>?
        ResolvePrevious(SurveyRequestIdempotencyResolution resolution) =>
        resolution.Outcome switch
        {
            SurveyRequestIdempotencyOutcome.NewSubmission => null,
            SurveyRequestIdempotencyOutcome.Replay =>
                Result.Success(MapResponse(resolution.ExistingResponse!)),
            SurveyRequestIdempotencyOutcome.PayloadMismatch =>
                Result.Failure<SubmitNewFarmSurveyRequestResponse>(
                    SurveyRequestError.IdempotencyPayloadMismatch()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution.Outcome,
                "Unsupported idempotency outcome.")
        };

    private static SubmitNewFarmSurveyRequestResponse MapResponse(
        SurveyRequest request) =>
        new(
            request.Id,
            request.RequestNumber,
            request.Kind,
            request.Status,
            request.CreatedAt);

    private static SubmitNewFarmSurveyRequestResponse MapResponse(
        SurveyRequestAcknowledgementResponse response) =>
        new(
            response.Id,
            response.RequestNumber,
            response.Kind,
            response.Status,
            response.CreatedAt);

    private static bool HasCompleteApplicantProfile(
        TenantOwnerRequestReference owner)
    {
        if (string.IsNullOrWhiteSpace(owner.FullName) ||
            owner.FullName.Trim().Length > 150 ||
            string.IsNullOrWhiteSpace(owner.Email) ||
            owner.Email.Trim().Length > 320 ||
            !MailAddress.TryCreate(owner.Email.Trim(), out var address) ||
            !string.Equals(
                address.Address,
                owner.Email.Trim(),
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(owner.Phone) ||
            owner.Phone.Trim().Length > 30)
        {
            return false;
        }

        var phone = owner.Phone.Trim();
        var digits = 0;
        for (var index = 0; index < phone.Length; index++)
        {
            var character = phone[index];
            if (character is >= '0' and <= '9')
            {
                digits++;
                continue;
            }

            if (character == '+' && index == 0 ||
                character is ' ' or '-' or '(' or ')' or '.')
            {
                continue;
            }

            return false;
        }

        return digits is >= 7 and <= 15;
    }
}
