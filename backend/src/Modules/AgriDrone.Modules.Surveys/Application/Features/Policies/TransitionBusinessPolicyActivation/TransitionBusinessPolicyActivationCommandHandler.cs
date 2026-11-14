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

            try
            {
                await unitOfWork.ExecuteInTransactionAsync(
                    async ct =>
                    {
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

                        await unitOfWork.SaveChangesAsync(ct);
                        return true;
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

        Result<TransitionBusinessPolicyActivationResponse>? failureResult = null;
        TransitionBusinessPolicyActivationResponse? response = null;

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    var currentActivation = await policyRepository.GetActivationAsync(ct);
                    if (currentActivation is null)
                    {
                        failureResult = Result.Failure<TransitionBusinessPolicyActivationResponse>(
                            BusinessPolicyError.NotFound("Business policy activation"));
                        return false;
                    }

                    if (request.ExpectedLastDecisionId.HasValue &&
                        request.ExpectedLastDecisionId.Value != currentActivation.LastDecisionId)
                    {
                        failureResult = Result.Failure<TransitionBusinessPolicyActivationResponse>(
                            BusinessPolicyError.DecisionMismatch(
                                request.ExpectedLastDecisionId.Value,
                                currentActivation.LastDecisionId));
                        return false;
                    }

                    using var oldStateData = JsonSerializer.SerializeToDocument(new
                    {
                        currentActivation.ActiveBusinessPolicyVersionId,
                        currentActivation.PublicIntakeEnabled,
                        currentActivation.CaptureEnabled,
                        currentActivation.ReleaseEnabled,
                        currentActivation.LastDecisionId
                    });

                    BusinessPolicyActivationDecision transitionDecision;
                    try
                    {
                        transitionDecision = currentActivation.ApplyDecision(
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
                        failureResult = Result.Failure<TransitionBusinessPolicyActivationResponse>(
                            BusinessPolicyError.InvalidLifecycle(ex.Message));
                        return false;
                    }

                    using var newStateData = JsonSerializer.SerializeToDocument(new
                    {
                        currentActivation.ActiveBusinessPolicyVersionId,
                        currentActivation.PublicIntakeEnabled,
                        currentActivation.CaptureEnabled,
                        currentActivation.ReleaseEnabled,
                        DecisionId = transitionDecision.Id,
                        currentActivation.ReasonCode
                    });

                    policyRepository.AddActivationDecision(transitionDecision);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "BusinessPolicyActivation",
                        currentActivation.Id,
                        "TRANSITION_ACTIVATION",
                        oldStateData,
                        newStateData,
                        now);

                    await unitOfWork.SaveChangesAsync(ct);

                    response = new TransitionBusinessPolicyActivationResponse(
                        currentActivation.Id,
                        currentActivation.ActiveBusinessPolicyVersionId,
                        currentActivation.PublicIntakeEnabled,
                        currentActivation.CaptureEnabled,
                        currentActivation.ReleaseEnabled,
                        currentActivation.LastDecisionId,
                        currentActivation.ActivatedAt,
                        currentActivation.ReasonCode,
                        currentActivation.EvidenceReference);

                    return true;
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

        if (failureResult is not null)
        {
            return failureResult;
        }

        return Result.Success(response!);
    }
}
