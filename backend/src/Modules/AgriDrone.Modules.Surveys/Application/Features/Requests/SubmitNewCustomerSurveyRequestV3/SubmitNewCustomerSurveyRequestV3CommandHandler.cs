using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Requests;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Application.Services;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequestV3;

internal sealed class SubmitNewCustomerSurveyRequestV3CommandHandler(
    ISurveyRequestRepository requestRepository,
    ISurveyServiceRepository serviceRepository,
    IBusinessPolicyRepository businessPolicyRepository,
    ISurveyRequestIdempotencyResolver idempotencyResolver,
    ISurveyRequestNumberGenerator requestNumberGenerator,
    ISurveyIntegrationOutbox integrationOutbox,
    ISurveysUnitOfWork unitOfWork,
    ITrackingTokenProtector tokenProtector,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        SubmitNewCustomerSurveyRequestV3Command,
        Result<SubmitNewCustomerSurveyRequestV3Response>>
{
    public async Task<Result<SubmitNewCustomerSurveyRequestV3Response>> Handle(
        SubmitNewCustomerSurveyRequestV3Command request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
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
                request.TenantCode,
                request.TenantName,
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
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.InvalidInput(
                    "The survey request payload is invalid."));
        }

        var idempotency = await idempotencyResolver.ResolveAsync(
            request.Idempotency,
            submission,
            cancellationToken);

        if (idempotency.Outcome == SurveyRequestIdempotencyOutcome.PayloadMismatch)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.IdempotencyPayloadMismatch());
        }

        if (idempotency.Outcome == SurveyRequestIdempotencyOutcome.Replay)
        {
            var existingRequest = await requestRepository.GetByIdAsync(
                idempotency.ExistingResponse!.Id,
                cancellationToken);

            if (existingRequest is not null)
            {
                var existingTracking = await requestRepository.GetTrackingByRequestIdAsync(
                    existingRequest.Id,
                    cancellationToken);

                var existingQuote = await requestRepository.GetQuoteSnapshotByRequestIdAsync(
                    existingRequest.Id,
                    cancellationToken);

                if (existingTracking is not null && existingQuote is not null)
                {
                    var replayedToken = tokenProtector.Unprotect(existingTracking.EncryptedToken);
                    var quoteResponse = MapQuoteResponse(existingQuote);

                    return Result.Success(new SubmitNewCustomerSurveyRequestV3Response(
                        existingRequest.Id,
                        existingRequest.RequestNumber,
                        existingRequest.TenantCode!,
                        existingRequest.TenantName ?? existingRequest.ApplicantName,
                        existingRequest.Kind,
                        existingRequest.Status,
                        replayedToken,
                        quoteResponse,
                        existingRequest.CreatedAt));
                }
            }
        }

        var service = await serviceRepository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);

        if (service is null ||
            service.Scope != ContractScope.V3 ||
            service.Status != SurveyServiceStatus.Active)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var now = timeProvider.GetUtcNow();

        var activation = await businessPolicyRepository.GetActivationAsync(cancellationToken);
        if (activation is null)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        if (!activation.PublicIntakeEnabled || !activation.CaptureEnabled || !activation.ReleaseEnabled)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.PublicIntakeDisabled());
        }

        var bundle = await businessPolicyRepository.GetBundleVersionByIdAsync(
            activation.ActiveBusinessPolicyVersionId,
            cancellationToken);

        if (bundle is null || !bundle.IsEffectiveAt(now))
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var deposit = await businessPolicyRepository.GetDepositPolicyByIdAsync(
            bundle.SurveyDepositPolicyId,
            cancellationToken);

        if (deposit is null || !deposit.IsEffectiveAt(now))
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var revision = await businessPolicyRepository.GetEffectiveRevisionAsync(
            service.Id,
            now,
            cancellationToken);

        if (revision is null || !bundle.ServiceRevisionIds.Contains(revision.Id))
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        var price = await businessPolicyRepository.GetEffectivePriceV3Async(
            service.Id,
            now,
            cancellationToken);

        if (price is null || !price.VersionNumber.HasValue || !price.PricePerPole.HasValue)
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.ServiceUnavailable());
        }

        SurveyRequest surveyRequest;
        try
        {
            surveyRequest = SurveyRequest.CreateNewCustomer(
                requestNumberGenerator.Create(),
                service,
                request.Idempotency,
                submission.TenantCode!,
                submission.TenantName!,
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
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
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

        var trackingToken = tokenProtector.GenerateToken();
        var trackingHash = tokenProtector.ComputeHash(trackingToken);
        var encryptedToken = tokenProtector.Protect(trackingToken);
        var tracking = SurveyRequestTracking.Create(
            surveyRequest.Id,
            trackingHash,
            encryptedToken,
            now);

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            surveyRequest.RequestNumber,
            surveyRequest.TenantCode,
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
                    requestRepository.AddQuoteSnapshot(quoteSnapshot);
                    requestRepository.AddTracking(tracking);

                    unitOfWork.AddAuditLog(AuditLog.ForSystemAction(
                        executionContext.CorrelationId,
                        "SurveyRequest",
                        surveyRequest.Id,
                        "PUBLIC_V3_SUBMIT",
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
                request.Idempotency,
                submission,
                cancellationToken);

            if (raceResolution.Outcome == SurveyRequestIdempotencyOutcome.Replay)
            {
                var existingTracking = await requestRepository.GetTrackingByRequestIdAsync(
                    raceResolution.ExistingResponse!.Id,
                    cancellationToken);

                var existingQuote = await requestRepository.GetQuoteSnapshotByRequestIdAsync(
                    raceResolution.ExistingResponse.Id,
                    cancellationToken);

                if (existingTracking is not null && existingQuote is not null)
                {
                    return Result.Success(new SubmitNewCustomerSurveyRequestV3Response(
                        raceResolution.ExistingResponse.Id,
                        raceResolution.ExistingResponse.RequestNumber,
                        raceResolution.ExistingResponse.TenantCode!,
                        raceResolution.ExistingResponse.TenantName ?? string.Empty,
                        raceResolution.ExistingResponse.Kind,
                        raceResolution.ExistingResponse.Status,
                        tokenProtector.Unprotect(existingTracking.EncryptedToken),
                        MapQuoteResponse(existingQuote),
                        raceResolution.ExistingResponse.CreatedAt));
                }
            }

            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.Duplicate());
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                SurveyRequestPersistenceConstraints.RequestNumber))
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.RequestNumberConflict());
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(
                SurveyRequestPersistenceConstraints.ActiveTenantCode))
        {
            return Result.Failure<SubmitNewCustomerSurveyRequestV3Response>(
                SurveyRequestError.TenantCodeUnavailable(surveyRequest.TenantCode!));
        }

        return Result.Success(new SubmitNewCustomerSurveyRequestV3Response(
            surveyRequest.Id,
            surveyRequest.RequestNumber,
            surveyRequest.TenantCode!,
            surveyRequest.TenantName ?? surveyRequest.ApplicantName,
            surveyRequest.Kind,
            surveyRequest.Status,
            trackingToken,
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
