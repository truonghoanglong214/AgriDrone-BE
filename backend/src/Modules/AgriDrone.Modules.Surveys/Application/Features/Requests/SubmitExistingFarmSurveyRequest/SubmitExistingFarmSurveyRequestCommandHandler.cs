using System.Text.Json;
using AgriDrone.IntegrationContracts.Farms;
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

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitExistingFarmSurveyRequest;

internal sealed class SubmitExistingFarmSurveyRequestCommandHandler(
    ISurveyRequestFarmReferenceQuery farmReferenceQuery,
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
        SubmitExistingFarmSurveyRequestCommand,
        Result<SubmitExistingFarmSurveyRequestResponse>>
{
    public async Task<Result<SubmitExistingFarmSurveyRequestResponse>> Handle(
        SubmitExistingFarmSurveyRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.TenantId is not Guid currentTenantId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                SurveyRequestError.CurrentTenantOwnerRequired());
        }

        var farm = await farmReferenceQuery.GetAsync(
            request.FarmId,
            cancellationToken);
        if (farm is null ||
            !farm.IsActive ||
            farm.TenantId != currentTenantId)
        {
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                SurveyRequestError.FarmUnavailable());
        }

        var owner = await tenantOwnerReferenceQuery.GetActiveOwnerAsync(
            farm.TenantId,
            actorId,
            cancellationToken);
        if (owner is null)
        {
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                SurveyRequestError.Forbidden());
        }

        if (!TenantOwnerApplicantProfileValidator.IsComplete(owner))
        {
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                SurveyRequestError.ApplicantProfileIncomplete());
        }

        if (!HasCompleteFarmProfile(farm))
        {
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                SurveyRequestError.FarmProfileIncomplete());
        }

        RequestIdempotency idempotency;
        SurveyRequestSubmissionSnapshot submission;
        try
        {
            idempotency = SurveyRequestIdempotencyFactory.ForTenantActor(
                farm.TenantId,
                actorId,
                request.IdempotencyKey);
            submission = SurveyRequestSubmissionSnapshot.Create(
                SurveyRequestKind.ExistingFarmSurvey,
                farm.TenantId,
                farm.FarmId,
                actorId,
                request.SurveyServiceId,
                owner.FullName,
                owner.Email,
                owner.Phone!,
                farm.Name,
                farm.Address!,
                farm.AreaHectares!.Value,
                farm.Longitude!.Value,
                farm.Latitude!.Value,
                farm.MapSrid!.Value,
                request.EstimatedPoleCount,
                request.PreferredStartAt,
                request.PreferredEndAt,
                request.Notes);
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
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
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
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
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                SurveyRequestError.ServiceUnavailable());
        }

        SurveyRequest surveyRequest;
        try
        {
            surveyRequest = SurveyRequest.CreateExistingFarmSurvey(
                farm.TenantId,
                farm.FarmId,
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
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
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
            surveyRequest.FarmId,
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
            farm.TenantId,
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
                        farm.TenantId,
                        farm.FarmId,
                        actorId,
                        executionContext.CorrelationId,
                        "SurveyRequest",
                        surveyRequest.Id,
                        "TENANT_OWNER_EXISTING_FARM_SURVEY_SUBMIT",
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
                Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                    SurveyRequestError.Duplicate());
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                SurveyRequestPersistenceConstraints.RequestNumber))
        {
            return Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                SurveyRequestError.RequestNumberConflict());
        }

        return Result.Success(MapResponse(surveyRequest));
    }

    private static Result<SubmitExistingFarmSurveyRequestResponse>?
        ResolvePrevious(SurveyRequestIdempotencyResolution resolution) =>
        resolution.Outcome switch
        {
            SurveyRequestIdempotencyOutcome.NewSubmission => null,
            SurveyRequestIdempotencyOutcome.Replay =>
                Result.Success(MapResponse(resolution.ExistingResponse!)),
            SurveyRequestIdempotencyOutcome.PayloadMismatch =>
                Result.Failure<SubmitExistingFarmSurveyRequestResponse>(
                    SurveyRequestError.IdempotencyPayloadMismatch()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution.Outcome,
                "Unsupported idempotency outcome.")
        };

    private static SubmitExistingFarmSurveyRequestResponse MapResponse(
        SurveyRequest request) =>
        new(
            request.Id,
            request.RequestNumber,
            request.Kind,
            request.Status,
            request.CreatedAt);

    private static SubmitExistingFarmSurveyRequestResponse MapResponse(
        SurveyRequestAcknowledgementResponse response) =>
        new(
            response.Id,
            response.RequestNumber,
            response.Kind,
            response.Status,
            response.CreatedAt);

    private static bool HasCompleteFarmProfile(
        SurveyRequestFarmReference farm)
    {
        if (string.IsNullOrWhiteSpace(farm.Name) ||
            farm.Name.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(farm.Address) ||
            farm.Address.Trim().Length > 2_000 ||
            farm.AreaHectares is not decimal area ||
            area <= 0m ||
            area >= 100_000_000m ||
            DecimalScale(area) > 4 ||
            farm.Longitude is not double longitude ||
            !double.IsFinite(longitude) ||
            longitude is < -180d or > 180d ||
            farm.Latitude is not double latitude ||
            !double.IsFinite(latitude) ||
            latitude is < -90d or > 90d ||
            farm.MapSrid != 4326)
        {
            return false;
        }

        return true;
    }

    private static int DecimalScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;
}
