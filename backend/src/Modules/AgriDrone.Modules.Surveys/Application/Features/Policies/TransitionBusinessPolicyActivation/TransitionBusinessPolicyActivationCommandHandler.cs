using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.TransitionBusinessPolicyActivation;

internal sealed class TransitionBusinessPolicyActivationCommandHandler(
    IBusinessPolicyRepository policyRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        TransitionBusinessPolicyActivationCommand,
        Result<TransitionBusinessPolicyActivationResponse>>
{
    public async Task<Result<TransitionBusinessPolicyActivationResponse>> Handle(
        TransitionBusinessPolicyActivationCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<TransitionBusinessPolicyActivationResponse>(
                BusinessPolicyError.CurrentUserRequired());
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    await unitOfWork.AcquireBusinessPolicyWriteLockAsync(ct);
                    return await TransitionLockedAsync(request, actorId, ct);
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<TransitionBusinessPolicyActivationResponse>(
                BusinessPolicyError.ConcurrentUpdate());
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure<TransitionBusinessPolicyActivationResponse>(
                BusinessPolicyError.ConcurrentUpdate(ex.Message));
        }
    }

    private async Task<Result<TransitionBusinessPolicyActivationResponse>> TransitionLockedAsync(
        TransitionBusinessPolicyActivationCommand request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var bundle = await policyRepository.GetBundleVersionByIdAsync(
            request.BusinessPolicyVersionId,
            cancellationToken);

        if (bundle is null || !bundle.IsEffectiveAt(now))
        {
            return Result.Failure<TransitionBusinessPolicyActivationResponse>(
                BusinessPolicyError.InactiveDependency("business policy version bundle"));
        }

        var activation = await policyRepository.GetActivationAsync(cancellationToken);

        if (activation is null)
        {
            if (request.ExpectedLastDecisionId.HasValue &&
                request.ExpectedLastDecisionId.Value != Guid.Empty)
            {
                return Result.Failure<TransitionBusinessPolicyActivationResponse>(
                    BusinessPolicyError.ConcurrentUpdate(
                        "No activation decision exists yet, but an expected decision was provided."));
            }

            var initialDecisionId = Guid.NewGuid();
            BusinessPolicyActivation newActivation;
            BusinessPolicyActivationDecision initialDecision;

            try
            {
                (newActivation, initialDecision) = BusinessPolicyActivation.CreateInitial(
                    activeBusinessPolicyVersionId: bundle.Id,
                    initialDecisionId: initialDecisionId,
                    activatedBy: actorId,
                    activatedAt: now,
                    reasonCode: request.ReasonCode,
                    evidenceReference: request.EvidenceReference);
            }
            catch (ArgumentException ex)
            {
                return Result.Failure<TransitionBusinessPolicyActivationResponse>(
                    BusinessPolicyError.InvalidLifecycle(ex.Message));
            }

            using var createData = JsonSerializer.SerializeToDocument(new
            {
                ActivationId = newActivation.Id,
                newActivation.ActiveBusinessPolicyVersionId,
                newActivation.PublicIntakeEnabled,
                newActivation.CaptureEnabled,
                newActivation.ReleaseEnabled,
                newActivation.LastDecisionId,
                newActivation.ReasonCode
            });

            policyRepository.AddActivation(newActivation);
            policyRepository.AddActivationDecision(initialDecision);

            auditWriter.AddSystemAdminAction(
                unitOfWork,
                actorId,
                executionContext.CorrelationId,
                "BusinessPolicyActivation",
                newActivation.Id,
                "INITIAL_ACTIVATION",
                oldData: null,
                createData,
                now);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new TransitionBusinessPolicyActivationResponse(
                newActivation.Id,
                newActivation.ActiveBusinessPolicyVersionId,
                newActivation.PublicIntakeEnabled,
                newActivation.CaptureEnabled,
                newActivation.ReleaseEnabled,
                newActivation.LastDecisionId,
                newActivation.ActivatedAt,
                newActivation.ReasonCode,
                newActivation.EvidenceReference));
        }

        if (request.ExpectedLastDecisionId.HasValue &&
            request.ExpectedLastDecisionId.Value != activation.LastDecisionId)
        {
            return Result.Failure<TransitionBusinessPolicyActivationResponse>(
                BusinessPolicyError.DecisionMismatch(
                    request.ExpectedLastDecisionId.Value,
                    activation.LastDecisionId));
        }

        using var oldStateData = JsonSerializer.SerializeToDocument(new
        {
            activation.ActiveBusinessPolicyVersionId,
            activation.PublicIntakeEnabled,
            activation.CaptureEnabled,
            activation.ReleaseEnabled,
            activation.LastDecisionId
        });

        BusinessPolicyActivationDecision transitionDecision;
        try
        {
            transitionDecision = activation.ApplyDecision(
                decisionId: Guid.NewGuid(),
                activeBusinessPolicyVersionId: bundle.Id,
                publicIntakeEnabled: request.PublicIntakeEnabled,
                captureEnabled: request.CaptureEnabled,
                releaseEnabled: request.ReleaseEnabled,
                changedBy: actorId,
                changedAt: now,
                reasonCode: request.ReasonCode,
                evidenceReference: request.EvidenceReference);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<TransitionBusinessPolicyActivationResponse>(
                BusinessPolicyError.InvalidLifecycle(ex.Message));
        }

        using var newStateData = JsonSerializer.SerializeToDocument(new
        {
            activation.ActiveBusinessPolicyVersionId,
            activation.PublicIntakeEnabled,
            activation.CaptureEnabled,
            activation.ReleaseEnabled,
            DecisionId = transitionDecision.Id,
            activation.ReasonCode
        });

        policyRepository.AddActivationDecision(transitionDecision);

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            "BusinessPolicyActivation",
            activation.Id,
            "TRANSITION_ACTIVATION",
            oldStateData,
            newStateData,
            now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TransitionBusinessPolicyActivationResponse(
            activation.Id,
            activation.ActiveBusinessPolicyVersionId,
            activation.PublicIntakeEnabled,
            activation.CaptureEnabled,
            activation.ReleaseEnabled,
            activation.LastDecisionId,
            activation.ActivatedAt,
            activation.ReasonCode,
            activation.EvidenceReference));
    }
}
