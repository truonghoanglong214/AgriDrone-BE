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

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishBusinessPolicyVersion;

internal sealed class PublishBusinessPolicyVersionCommandHandler(
    IBusinessPolicyRepository policyRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        PublishBusinessPolicyVersionCommand,
        Result<PublishBusinessPolicyVersionResponse>>
{
    public async Task<Result<PublishBusinessPolicyVersionResponse>> Handle(
        PublishBusinessPolicyVersionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PublishBusinessPolicyVersionResponse>(
                BusinessPolicyError.CurrentUserRequired());
        }

        var now = timeProvider.GetUtcNow();

        // 1. Verify dependencies exist and are effective
        var depositPolicy = await policyRepository.GetDepositPolicyByIdAsync(
            request.SurveyDepositPolicyId,
            cancellationToken);

        if (depositPolicy is null || !depositPolicy.IsEffectiveAt(now))
        {
            return Result.Failure<PublishBusinessPolicyVersionResponse>(
                BusinessPolicyError.InactiveDependency("survey deposit policy"));
        }

        var claimPolicy = await policyRepository.GetClaimPolicyByIdAsync(
            request.ClaimPolicyId,
            cancellationToken);

        if (claimPolicy is null || !claimPolicy.IsEffectiveAt(now))
        {
            return Result.Failure<PublishBusinessPolicyVersionResponse>(
                BusinessPolicyError.InactiveDependency("claim policy"));
        }

        var cancellationPolicy = await policyRepository.GetCancellationPolicyByIdAsync(
            request.CancellationPolicyId,
            cancellationToken);

        if (cancellationPolicy is null || !cancellationPolicy.IsEffectiveAt(now))
        {
            return Result.Failure<PublishBusinessPolicyVersionResponse>(
                BusinessPolicyError.InactiveDependency("cancellation policy"));
        }

        foreach (var revisionId in request.ServiceRevisionIds)
        {
            var revision = await policyRepository.GetRevisionByIdAsync(
                revisionId,
                cancellationToken);

            if (revision is null || !revision.IsEffectiveAt(now))
            {
                return Result.Failure<PublishBusinessPolicyVersionResponse>(
                    BusinessPolicyError.InactiveDependency($"service revision '{revisionId}'"));
            }
        }

        // 2. Concurrency check on stream key
        var latestBundle = await policyRepository.GetLatestBundleVersionAsync(
            request.StreamKey,
            cancellationToken);

        var currentLatestVersion = latestBundle?.VersionNumber ?? 0;
        if (request.ExpectedLatestVersion.HasValue &&
            request.ExpectedLatestVersion.Value != currentLatestVersion)
        {
            return Result.Failure<PublishBusinessPolicyVersionResponse>(
                BusinessPolicyError.PredecessorMismatch(
                    request.ExpectedLatestVersion.Value,
                    currentLatestVersion));
        }

        var nextVersionNumber = currentLatestVersion + 1;
        var predecessorId = latestBundle?.Id;

        if (latestBundle is not null && !latestBundle.EffectiveTo.HasValue)
        {
            try
            {
                latestBundle.CloseAt(now);
            }
            catch (SurveyDomainException ex)
            {
                return Result.Failure<PublishBusinessPolicyVersionResponse>(
                    BusinessPolicyError.ConcurrentUpdate(ex.Message));
            }
        }

        BusinessPolicyVersion newBundle;
        try
        {
            newBundle = BusinessPolicyVersion.Create(
                id: Guid.NewGuid(),
                streamKey: request.StreamKey,
                versionNumber: nextVersionNumber,
                predecessorVersionId: predecessorId,
                surveyDepositPolicyId: request.SurveyDepositPolicyId,
                claimPolicyId: request.ClaimPolicyId,
                cancellationPolicyId: request.CancellationPolicyId,
                serviceRevisionIds: request.ServiceRevisionIds,
                pricingCurrency: CurrencyCode.Vnd,
                effectiveFrom: now,
                effectiveTo: null,
                createdBy: actorId,
                createdAt: now,
                reasonCode: request.ReasonCode,
                evidenceReference: request.EvidenceReference);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PublishBusinessPolicyVersionResponse>(
                BusinessPolicyError.InvalidLifecycle(ex.Message));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            PredecessorBundleId = predecessorId,
            PredecessorVersion = currentLatestVersion
        });

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            BundleId = newBundle.Id,
            newBundle.StreamKey,
            newBundle.VersionNumber,
            newBundle.PolicyFingerprint,
            newBundle.EffectiveFrom,
            newBundle.ReasonCode
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    policyRepository.AddBundleVersion(newBundle);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "BusinessPolicyVersion",
                        newBundle.Id,
                        "PUBLISH_BUNDLE",
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
            return Result.Failure<PublishBusinessPolicyVersionResponse>(
                BusinessPolicyError.ConcurrentUpdate());
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure<PublishBusinessPolicyVersionResponse>(
                BusinessPolicyError.ConcurrentUpdate(ex.Message));
        }

        return Result.Success(new PublishBusinessPolicyVersionResponse(
            newBundle.Id,
            newBundle.StreamKey,
            newBundle.VersionNumber,
            newBundle.PredecessorVersionId,
            newBundle.SurveyDepositPolicyId,
            newBundle.ClaimPolicyId,
            newBundle.CancellationPolicyId,
            newBundle.ServiceRevisionIds,
            newBundle.PricingCurrency,
            newBundle.PolicyFingerprint,
            newBundle.EffectiveFrom,
            newBundle.EffectiveTo,
            newBundle.ReasonCode,
            newBundle.EvidenceReference));
    }
}
