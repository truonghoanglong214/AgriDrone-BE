using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishCancellationPolicy;

internal sealed class PublishCancellationPolicyCommandHandler(
    IBusinessPolicyRepository policyRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        PublishCancellationPolicyCommand,
        Result<PublishCancellationPolicyResponse>>
{
    public async Task<Result<PublishCancellationPolicyResponse>> Handle(
        PublishCancellationPolicyCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PublishCancellationPolicyResponse>(
                BusinessPolicyError.CurrentUserRequired());
        }

        var latestPolicy = await policyRepository.GetLatestCancellationPolicyAsync(
            cancellationToken);

        var currentLatestVersion = latestPolicy?.VersionNumber ?? 0;
        if (request.ExpectedLatestVersion.HasValue &&
            request.ExpectedLatestVersion.Value != currentLatestVersion)
        {
            return Result.Failure<PublishCancellationPolicyResponse>(
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
                return Result.Failure<PublishCancellationPolicyResponse>(
                    BusinessPolicyError.ConcurrentUpdate(ex.Message));
            }
        }

        CancellationPolicy newPolicy;
        try
        {
            newPolicy = CancellationPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: nextVersionNumber,
                predecessorVersionId: predecessorId,
                refundableBeforeCapture: request.RefundableBeforeCapture,
                retainDepositAfterCapture: request.RetainDepositAfterCapture,
                allowedFreeNoShowReschedules: request.AllowedFreeNoShowReschedules,
                operationalPostponementRefundable: request.OperationalPostponementRefundable,
                serviceFailureRefundable: request.ServiceFailureRefundable,
                effectiveFrom: now,
                effectiveTo: null,
                createdBy: actorId,
                createdAt: now,
                reasonCode: request.ReasonCode,
                evidenceReference: request.EvidenceReference);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PublishCancellationPolicyResponse>(
                BusinessPolicyError.InvalidLifecycle(ex.Message));
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
            newPolicy.RefundableBeforeCapture,
            newPolicy.RetainDepositAfterCapture,
            newPolicy.AllowedFreeNoShowReschedules,
            newPolicy.EffectiveFrom,
            newPolicy.ReasonCode
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    policyRepository.AddCancellationPolicy(newPolicy);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "CancellationPolicy",
                        newPolicy.Id,
                        "PUBLISH_CANCELLATION_POLICY",
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
            return Result.Failure<PublishCancellationPolicyResponse>(
                BusinessPolicyError.ConcurrentUpdate());
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure<PublishCancellationPolicyResponse>(
                BusinessPolicyError.ConcurrentUpdate(ex.Message));
        }

        return Result.Success(new PublishCancellationPolicyResponse(
            newPolicy.Id,
            newPolicy.VersionNumber,
            newPolicy.PredecessorVersionId,
            newPolicy.RefundableBeforeCapture,
            newPolicy.RetainDepositAfterCapture,
            newPolicy.AllowedFreeNoShowReschedules,
            newPolicy.OperationalPostponementRefundable,
            newPolicy.ServiceFailureRefundable,
            newPolicy.EffectiveFrom,
            newPolicy.EffectiveTo,
            newPolicy.ReasonCode,
            newPolicy.EvidenceReference));
    }
}
