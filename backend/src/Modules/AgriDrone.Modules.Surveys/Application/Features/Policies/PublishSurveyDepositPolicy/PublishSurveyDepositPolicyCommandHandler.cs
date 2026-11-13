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

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishSurveyDepositPolicy;

internal sealed class PublishSurveyDepositPolicyCommandHandler(
    IBusinessPolicyRepository policyRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        PublishSurveyDepositPolicyCommand,
        Result<PublishSurveyDepositPolicyResponse>>
{
    public async Task<Result<PublishSurveyDepositPolicyResponse>> Handle(
        PublishSurveyDepositPolicyCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PublishSurveyDepositPolicyResponse>(
                BusinessPolicyError.CurrentUserRequired());
        }

        var latestPolicy = await policyRepository.GetLatestDepositPolicyAsync(
            cancellationToken);

        var currentLatestVersion = latestPolicy?.VersionNumber ?? 0;
        if (request.ExpectedLatestVersion.HasValue &&
            request.ExpectedLatestVersion.Value != currentLatestVersion)
        {
            return Result.Failure<PublishSurveyDepositPolicyResponse>(
                BusinessPolicyError.PredecessorMismatch(
                    request.ExpectedLatestVersion.Value,
                    currentLatestVersion));
        }

        var now = timeProvider.GetUtcNow();
        var nextVersionNumber = currentLatestVersion + 1;
        var predecessorId = latestPolicy?.Id;

        if (latestPolicy is not null && !latestPolicy.EffectiveTo.HasValue)
        {
            try
            {
                latestPolicy.CloseAt(now);
            }
            catch (SurveyDomainException ex)
            {
                return Result.Failure<PublishSurveyDepositPolicyResponse>(
                    BusinessPolicyError.ConcurrentUpdate(ex.Message));
            }
        }

        SurveyDepositPolicy newPolicy;
        try
        {
            newPolicy = SurveyDepositPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: nextVersionNumber,
                predecessorVersionId: predecessorId,
                amount: request.Amount,
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
            return Result.Failure<PublishSurveyDepositPolicyResponse>(
                BusinessPolicyError.InvalidAmount(ex.Message));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            PredecessorPolicyId = predecessorId,
            PredecessorVersion = currentLatestVersion
        });

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            PolicyId = newPolicy.Id,
            newPolicy.VersionNumber,
            newPolicy.Amount,
            newPolicy.Currency,
            newPolicy.EffectiveFrom,
            newPolicy.ReasonCode
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    policyRepository.AddDepositPolicy(newPolicy);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "SurveyDepositPolicy",
                        newPolicy.Id,
                        "PUBLISH_DEPOSIT_POLICY",
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
            return Result.Failure<PublishSurveyDepositPolicyResponse>(
                BusinessPolicyError.ConcurrentUpdate());
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure<PublishSurveyDepositPolicyResponse>(
                BusinessPolicyError.ConcurrentUpdate(ex.Message));
        }

        return Result.Success(new PublishSurveyDepositPolicyResponse(
            newPolicy.Id,
            newPolicy.VersionNumber,
            newPolicy.PredecessorVersionId,
            newPolicy.Amount,
            newPolicy.Currency,
            newPolicy.EffectiveFrom,
            newPolicy.EffectiveTo,
            newPolicy.ReasonCode,
            newPolicy.EvidenceReference));
    }
}
