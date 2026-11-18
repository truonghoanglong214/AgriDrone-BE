using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServicePriceV3;

internal sealed class PublishSurveyServicePriceV3CommandHandler(
    ISurveyServiceRepository serviceRepository,
    IBusinessPolicyRepository policyRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        PublishSurveyServicePriceV3Command,
        Result<PublishSurveyServicePriceV3Response>>
{
    public async Task<Result<PublishSurveyServicePriceV3Response>> Handle(
        PublishSurveyServicePriceV3Command request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PublishSurveyServicePriceV3Response>(
                BusinessPolicyError.CurrentUserRequired());
        }

        var service = await serviceRepository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);

        if (service is null)
        {
            return Result.Failure<PublishSurveyServicePriceV3Response>(
                BusinessPolicyError.NotFound("Survey service"));
        }

        if (service.Scope != ContractScope.V3)
        {
            return Result.Failure<PublishSurveyServicePriceV3Response>(
                BusinessPolicyError.InvalidLifecycle(
                    "Only V3 survey services support V3 per-pole versioned prices."));
        }

        if (service.Status == SurveyServiceStatus.Retired)
        {
            return Result.Failure<PublishSurveyServicePriceV3Response>(
                BusinessPolicyError.InvalidLifecycle(
                    "A retired survey service cannot receive a new price."));
        }

        var latestPrice = await policyRepository.GetLatestPriceV3Async(
            service.Id,
            cancellationToken);

        var currentLatestVersion = latestPrice?.VersionNumber ?? 0;
        if (request.ExpectedLatestPriceVersion.HasValue &&
            request.ExpectedLatestPriceVersion.Value != currentLatestVersion)
        {
            return Result.Failure<PublishSurveyServicePriceV3Response>(
                BusinessPolicyError.PredecessorMismatch(
                    request.ExpectedLatestPriceVersion.Value,
                    currentLatestVersion));
        }

        var now = timeProvider.GetUtcNow();
        var nextVersionNumber = currentLatestVersion + 1;
        var predecessorId = latestPrice?.Id;

        if (latestPrice is not null && !latestPrice.EffectiveTo.HasValue)
        {
            try
            {
                latestPrice.CloseAt(now);
            }
            catch (SurveyDomainException ex)
            {
                return Result.Failure<PublishSurveyServicePriceV3Response>(
                    BusinessPolicyError.ConcurrentUpdate(ex.Message));
            }
        }

        SurveyServicePrice newPrice;
        try
        {
            newPrice = SurveyServicePrice.CreatePerPoleV3(
                id: Guid.NewGuid(),
                surveyServiceId: service.Id,
                versionNumber: nextVersionNumber,
                predecessorVersionId: predecessorId,
                pricePerPoleAmount: request.AmountPerPole,
                currency: CurrencyCode.Vnd,
                effectiveFrom: now,
                effectiveTo: null,
                createdBy: actorId,
                createdAt: now,
                reasonCode: request.ReasonCode,
                evidenceReference: request.EvidenceReference);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PublishSurveyServicePriceV3Response>(
                BusinessPolicyError.InvalidAmount(ex.Message));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            PredecessorPriceId = predecessorId,
            PredecessorVersion = currentLatestVersion
        });

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            PriceId = newPrice.Id,
            newPrice.SurveyServiceId,
            newPrice.VersionNumber,
            AmountPerPole = newPrice.PricePerPole?.Amount,
            newPrice.Currency,
            newPrice.EffectiveFrom,
            newPrice.ReasonCode
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    policyRepository.AddPriceV3(newPrice);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "SurveyServicePrice",
                        newPrice.Id,
                        "PUBLISH_PRICE_V3",
                        oldData,
                        newData,
                        now);

                    await unitOfWork.SaveChangesAsync(ct);
                    return true;
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PublishSurveyServicePriceV3Response>(
                BusinessPolicyError.ConcurrentUpdate());
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure<PublishSurveyServicePriceV3Response>(
                BusinessPolicyError.ConcurrentUpdate(ex.Message));
        }

        return Result.Success(new PublishSurveyServicePriceV3Response(
            newPrice.Id,
            newPrice.SurveyServiceId,
            newPrice.VersionNumber!.Value,
            newPrice.PredecessorVersionId,
            newPrice.PricePerPole!.Value.Amount,
            newPrice.Currency,
            newPrice.EffectiveFrom,
            newPrice.EffectiveTo,
            newPrice.ReasonCode!,
            newPrice.EvidenceReference));
    }
}
