using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishClaimPolicy;

internal sealed class PublishClaimPolicyCommandHandler(
    IBusinessPolicyRepository policyRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        PublishClaimPolicyCommand,
        Result<PublishClaimPolicyResponse>>
{
    public async Task<Result<PublishClaimPolicyResponse>> Handle(
        PublishClaimPolicyCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PublishClaimPolicyResponse>(
                BusinessPolicyError.CurrentUserRequired());
        }

        var latestPolicy = await policyRepository.GetLatestClaimPolicyAsync(
            cancellationToken);

        var currentLatestVersion = latestPolicy?.VersionNumber ?? 0;
        if (request.ExpectedLatestVersion.HasValue &&
            request.ExpectedLatestVersion.Value != currentLatestVersion)
        {
            return Result.Failure<PublishClaimPolicyResponse>(
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
                return Result.Failure<PublishClaimPolicyResponse>(
                    BusinessPolicyError.ConcurrentUpdate(ex.Message));
            }
        }

        ClaimPolicy newPolicy;
        try
        {
            newPolicy = ClaimPolicy.Create(
                id: Guid.NewGuid(),
                versionNumber: nextVersionNumber,
                predecessorVersionId: predecessorId,
                calendarWindowDays: request.CalendarWindowDays,
                timeZoneId: request.TimeZoneId,
                excludeNoticeDate: request.ExcludeNoticeDate,
                effectiveFrom: now,
                effectiveTo: null,
                createdBy: actorId,
                createdAt: now,
                reasonCode: request.ReasonCode,
                evidenceReference: request.EvidenceReference);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PublishClaimPolicyResponse>(
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
            newPolicy.CalendarWindowDays,
            newPolicy.TimeZoneId,
            newPolicy.ExcludeNoticeDate,
            newPolicy.EffectiveFrom,
            newPolicy.ReasonCode
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    policyRepository.AddClaimPolicy(newPolicy);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "ClaimPolicy",
                        newPolicy.Id,
                        "PUBLISH_CLAIM_POLICY",
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
            return Result.Failure<PublishClaimPolicyResponse>(
                BusinessPolicyError.ConcurrentUpdate());
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure<PublishClaimPolicyResponse>(
                BusinessPolicyError.ConcurrentUpdate(ex.Message));
        }

        return Result.Success(new PublishClaimPolicyResponse(
            newPolicy.Id,
            newPolicy.VersionNumber,
            newPolicy.PredecessorVersionId,
            newPolicy.CalendarWindowDays,
            newPolicy.TimeZoneId,
            newPolicy.ExcludeNoticeDate,
            newPolicy.EffectiveFrom,
            newPolicy.EffectiveTo,
            newPolicy.ReasonCode,
            newPolicy.EvidenceReference));
    }
}
