using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.IntegrationContracts.Identity;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
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

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitTenantOwnerSurveyRequestV3;

internal sealed class SubmitTenantOwnerSurveyRequestV3CommandHandler(
    ISurveyRequestFarmReferenceQuery farmReferenceQuery,
    ITenantOwnerRequestReferenceQuery tenantOwnerReferenceQuery,
    ISurveyRequestRepository requestRepository,
    ISurveyServiceRepository serviceRepository,
    IBusinessPolicyRepository businessPolicyRepository,
    ISurveyRequestIdempotencyResolver idempotencyResolver,
    ISurveyRequestNumberGenerator requestNumberGenerator,
    ISurveyIntegrationOutbox integrationOutbox,
    ISurveysUnitOfWork unitOfWork,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    IV3FollowUpBaselineQuery v3FollowUpBaselineQuery)
    : IRequestHandler<
        SubmitTenantOwnerSurveyRequestV3Command,
        Result<SubmitTenantOwnerSurveyRequestV3Response>>
{
    public async Task<Result<SubmitTenantOwnerSurveyRequestV3Response>> Handle(
        SubmitTenantOwnerSurveyRequestV3Command request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.TenantId is not Guid currentTenantId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.CurrentTenantOwnerRequired());
        }

        var owner = await tenantOwnerReferenceQuery.GetActiveOwnerAsync(
            currentTenantId,
            actorId,
            cancellationToken);

        if (owner is null)
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.Forbidden());
        }

        if (!TenantOwnerApplicantProfileValidator.IsComplete(owner))
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.ApplicantProfileIncomplete());
        }

        SurveyRequestKind kind;
        Guid? farmId = null;
        string farmName;
        string farmAddress;
        decimal approximateAreaHa;
        double longitude;
        double latitude;
        int mapSrid = 4326;

        if (request.FarmId.HasValue)
        {
            kind = SurveyRequestKind.ExistingFarmSurvey;
            farmId = request.FarmId.Value;

            var farm = await farmReferenceQuery.GetAsync(
                farmId.Value,
                cancellationToken);

            if (farm is null || !farm.IsActive)
            {
                return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                    SurveyRequestError.FarmUnavailable());
            }

            if (farm.TenantId != currentTenantId)
            {
                return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                    SurveyRequestError.FarmNotOwnedByTenant());
            }

            if (string.IsNullOrWhiteSpace(farm.Address) ||
                !farm.AreaHectares.HasValue ||
                !farm.Longitude.HasValue ||
                !farm.Latitude.HasValue ||
                !farm.MapSrid.HasValue)
            {
                return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                    SurveyRequestError.FarmProfileIncomplete());
            }

            farmName = farm.Name;
            farmAddress = farm.Address;
            approximateAreaHa = farm.AreaHectares.Value;
            longitude = farm.Longitude.Value;
            latitude = farm.Latitude.Value;
            mapSrid = farm.MapSrid.Value;
        }
        else
        {
            kind = SurveyRequestKind.ExistingTenantNewFarm;
            farmName = request.FarmName!;
            farmAddress = request.FarmAddress!;
            approximateAreaHa = request.ApproximateAreaHa!.Value;
            longitude = request.Longitude!.Value;
            latitude = request.Latitude!.Value;
        }

        var idempotency = SurveyRequestIdempotencyFactory.ForTenantActor(
            currentTenantId,
            actorId,
            request.IdempotencyKey);

        SurveyRequestSubmissionSnapshot submission;
        try
        {
            submission = SurveyRequestSubmissionSnapshot.Create(
                kind,
                currentTenantId,
                farmId,
                actorId,
                request.SurveyServiceId,
                tenantCode: null,
                tenantName: null,
                owner.FullName,
                owner.Email,
                owner.Phone!,
                farmName,
                farmAddress,
                approximateAreaHa,
                longitude,
                latitude,
                mapSrid,
                request.EstimatedPoleCount,
                request.PreferredStartAt,
                request.PreferredEndAt,
                request.Notes);
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.InvalidInput(
                    "The survey request payload is invalid."));
        }

        var idempotencyResolution = await idempotencyResolver.ResolveAsync(
            idempotency,
            submission,
            cancellationToken);

        if (idempotencyResolution.Outcome == SurveyRequestIdempotencyOutcome.PayloadMismatch)
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.IdempotencyPayloadMismatch());
        }

        if (idempotencyResolution.Outcome == SurveyRequestIdempotencyOutcome.Replay)
        {
            var existingRequest = await requestRepository.GetByIdAsync(
                idempotencyResolution.ExistingResponse!.Id,
                cancellationToken);

            if (existingRequest is not null)
            {
                var existingQuote = await requestRepository.GetQuoteSnapshotByRequestIdAsync(
                    existingRequest.Id,
                    cancellationToken);

                if (existingQuote is not null)
                {
                    return Result.Success(new SubmitTenantOwnerSurveyRequestV3Response(
                        existingRequest.Id,
                        existingRequest.RequestNumber,
                        existingRequest.TenantId!.Value,
                        existingRequest.FarmId,
                        existingRequest.Kind,
                        existingRequest.Status,
                        MapQuoteResponse(existingQuote),
                        existingRequest.CreatedAt));
                }
            }
        }

        if (farmId.HasValue)
        {
            var baselineEligibility = await v3FollowUpBaselineQuery.GetEligibleAsync(
                currentTenantId,
                farmId.Value,
                cancellationToken);

            if (!baselineEligibility.IsEligible)
            {
                return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                    SurveyRequestError.FollowUpBaselineRequired());
            }
        }

        var service = await serviceRepository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);

        if (service is null ||
            service.Scope != ContractScope.V3 ||
            service.Status != SurveyServiceStatus.Active)
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var now = timeProvider.GetUtcNow();

        var activation = await businessPolicyRepository.GetActivationAsync(cancellationToken);
        if (activation is null || !activation.CaptureEnabled || !activation.ReleaseEnabled)
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var bundle = await businessPolicyRepository.GetBundleVersionByIdAsync(
            activation.ActiveBusinessPolicyVersionId,
            cancellationToken);

        if (bundle is null || !bundle.IsEffectiveAt(now))
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var deposit = await businessPolicyRepository.GetDepositPolicyByIdAsync(
            bundle.SurveyDepositPolicyId,
            cancellationToken);

        if (deposit is null || !deposit.IsEffectiveAt(now))
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var revision = await businessPolicyRepository.GetEffectiveRevisionAsync(
            service.Id,
            now,
            cancellationToken);

        if (revision is null || !bundle.ServiceRevisionIds.Contains(revision.Id))
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var price = await businessPolicyRepository.GetEffectivePriceV3Async(
            service.Id,
            now,
            cancellationToken);

        if (price is null || !price.VersionNumber.HasValue || !price.PricePerPole.HasValue)
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        SurveyRequest surveyRequest;
        try
        {
            if (kind == SurveyRequestKind.ExistingFarmSurvey)
            {
                surveyRequest = SurveyRequest.CreateExistingFarmSurvey(
                    currentTenantId,
                    farmId!.Value,
                    actorId,
                    requestNumberGenerator.Create(),
                    service,
                    idempotency,
                    owner.FullName,
                    owner.Email,
                    owner.Phone!,
                    farmName,
                    farmAddress,
                    approximateAreaHa,
                    new Point(longitude, latitude) { SRID = mapSrid },
                    submission.EstimatedPoleCount,
                    submission.PreferredStartAt,
                    submission.PreferredEndAt,
                    submission.Notes,
                    now);
            }
            else
            {
                surveyRequest = SurveyRequest.CreateExistingTenantNewFarm(
                    currentTenantId,
                    actorId,
                    requestNumberGenerator.Create(),
                    service,
                    idempotency,
                    owner.FullName,
                    owner.Email,
                    owner.Phone!,
                    farmName,
                    farmAddress,
                    approximateAreaHa,
                    new Point(longitude, latitude) { SRID = mapSrid },
                    submission.EstimatedPoleCount,
                    submission.PreferredStartAt,
                    submission.PreferredEndAt,
                    submission.Notes,
                    now);
            }
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.InvalidInput(
                    "The survey request payload is invalid."));
        }

        var quoteSnapshot = SurveyRequestQuoteSnapshot.Create(
            Guid.NewGuid(),
            surveyRequest.Id,
            bundle.Id,
            bundle.PolicyFingerprint,
            revision.Id,
            revision.VersionNumber,
            price.Id,
            price.VersionNumber.Value,
            price.PricePerPole.Value.Amount,
            "VND",
            deposit.Id,
            deposit.VersionNumber,
            deposit.Amount,
            submission.EstimatedPoleCount,
            now,
            now);

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            surveyRequest.RequestNumber,
            surveyRequest.TenantId,
            surveyRequest.FarmId,
            surveyRequest.Kind,
            surveyRequest.Status,
            surveyRequest.SurveyServiceId,
            quoteSnapshot.BusinessPolicyVersionId,
            quoteSnapshot.PolicyFingerprint,
            quoteSnapshot.PricePerPole,
            quoteSnapshot.DepositAmount
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
                [EmailTemplateVariableKeys.ApplicantName] = surveyRequest.ApplicantName,
                [EmailTemplateVariableKeys.RequestNumber] = surveyRequest.RequestNumber,
                [EmailTemplateVariableKeys.SurveyServiceName] = service.Name
            });

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.EmailNotificationRequestedV1,
            messageId,
            executionContext.CorrelationId,
            tenantId: currentTenantId,
            actorId: actorId,
            now,
            acknowledgement);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    requestRepository.Add(surveyRequest);
                    requestRepository.AddQuoteSnapshot(quoteSnapshot);

                    unitOfWork.AddAuditLog(AuditLog.ForSystemAction(
                        executionContext.CorrelationId,
                        "SurveyRequest",
                        surveyRequest.Id,
                        "TENANT_OWNER_V3_SUBMIT",
                        oldData: null,
                        auditData,
                        now));

                    integrationOutbox.Add(
                        envelope,
                        surveyRequest.Id.ToString("D"));

                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);
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

            if (raceResolution.Outcome == SurveyRequestIdempotencyOutcome.Replay)
            {
                var existingQuote = await requestRepository.GetQuoteSnapshotByRequestIdAsync(
                    raceResolution.ExistingResponse!.Id,
                    cancellationToken);

                if (existingQuote is not null)
                {
                    return Result.Success(new SubmitTenantOwnerSurveyRequestV3Response(
                        raceResolution.ExistingResponse.Id,
                        raceResolution.ExistingResponse.RequestNumber,
                        currentTenantId,
                        farmId,
                        raceResolution.ExistingResponse.Kind,
                        raceResolution.ExistingResponse.Status,
                        MapQuoteResponse(existingQuote),
                        raceResolution.ExistingResponse.CreatedAt));
                }
            }

            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.Duplicate());
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                SurveyRequestPersistenceConstraints.RequestNumber))
        {
            return Result.Failure<SubmitTenantOwnerSurveyRequestV3Response>(
                SurveyRequestError.RequestNumberConflict());
        }

        return Result.Success(new SubmitTenantOwnerSurveyRequestV3Response(
            surveyRequest.Id,
            surveyRequest.RequestNumber,
            currentTenantId,
            farmId,
            surveyRequest.Kind,
            surveyRequest.Status,
            MapQuoteResponse(quoteSnapshot),
            surveyRequest.CreatedAt));
    }

    private static SurveyRequestQuoteResponse MapQuoteResponse(
        SurveyRequestQuoteSnapshot snapshot) =>
        new(
            snapshot.BusinessPolicyVersionId,
            snapshot.PolicyFingerprint,
            snapshot.SurveyServiceRevisionId,
            snapshot.SurveyServiceRevisionVersion,
            snapshot.PriceVersionId,
            snapshot.PriceVersionNumber,
            snapshot.PricePerPole,
            snapshot.Currency,
            snapshot.DepositPolicyId,
            snapshot.DepositVersionNumber,
            snapshot.DepositAmount,
            snapshot.EstimatedPoleCount,
            snapshot.IndicativeTotalAmount,
            snapshot.EvaluatedAt);
}
