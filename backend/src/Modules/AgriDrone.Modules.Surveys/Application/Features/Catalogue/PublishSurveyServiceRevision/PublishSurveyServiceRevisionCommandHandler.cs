using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServiceRevision;

internal sealed class PublishSurveyServiceRevisionCommandHandler(
    ISurveyServiceRepository serviceRepository,
    IBusinessPolicyRepository policyRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        PublishSurveyServiceRevisionCommand,
        Result<PublishSurveyServiceRevisionResponse>>
{
    public async Task<Result<PublishSurveyServiceRevisionResponse>> Handle(
        PublishSurveyServiceRevisionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PublishSurveyServiceRevisionResponse>(
                BusinessPolicyError.CurrentUserRequired());
        }

        var service = await serviceRepository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);

        if (service is null)
        {
            return Result.Failure<PublishSurveyServiceRevisionResponse>(
                BusinessPolicyError.NotFound("Survey service"));
        }

        if (service.Scope != ContractScope.V3)
        {
            return Result.Failure<PublishSurveyServiceRevisionResponse>(
                BusinessPolicyError.InvalidLifecycle(
                    "Only V3 survey services support versioned revisions."));
        }

        if (service.Status == SurveyServiceStatus.Retired)
        {
            return Result.Failure<PublishSurveyServiceRevisionResponse>(
                BusinessPolicyError.InvalidLifecycle(
                    "A retired survey service cannot receive a new revision."));
        }

        var latestRevision = await policyRepository.GetLatestRevisionAsync(
            service.Id,
            cancellationToken);

        var currentLatestVersion = latestRevision?.VersionNumber ?? 0;
        if (request.ExpectedLatestVersionNumber.HasValue &&
            request.ExpectedLatestVersionNumber.Value != currentLatestVersion)
        {
            return Result.Failure<PublishSurveyServiceRevisionResponse>(
                BusinessPolicyError.PredecessorMismatch(
                    request.ExpectedLatestVersionNumber.Value,
                    currentLatestVersion));
        }

        var now = timeProvider.GetUtcNow();
        var nextVersionNumber = currentLatestVersion + 1;
        var predecessorId = latestRevision?.Id;

        if (latestRevision is not null && !latestRevision.EffectiveTo.HasValue)
        {
            try
            {
                latestRevision.CloseAt(now);
            }
            catch (SurveyDomainException ex)
            {
                return Result.Failure<PublishSurveyServiceRevisionResponse>(
                    BusinessPolicyError.ConcurrentUpdate(ex.Message));
            }
        }

        SurveyServiceRevision newRevision;
        try
        {
            newRevision = SurveyServiceRevision.Create(
                id: Guid.NewGuid(),
                surveyServiceId: service.Id,
                versionNumber: nextVersionNumber,
                predecessorVersionId: predecessorId,
                name: request.Name,
                description: request.Description,
                mapsPoles: request.MapsPoles,
                screensDisease: request.ScreensDisease,
                effectiveFrom: now,
                effectiveTo: null,
                createdBy: actorId,
                createdAt: now,
                reasonCode: request.ReasonCode,
                evidenceReference: request.EvidenceReference);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PublishSurveyServiceRevisionResponse>(
                BusinessPolicyError.InvalidLifecycle(ex.Message));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            PredecessorRevisionId = predecessorId,
            PredecessorVersion = currentLatestVersion
        });

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            RevisionId = newRevision.Id,
            newRevision.SurveyServiceId,
            newRevision.VersionNumber,
            newRevision.Name,
            newRevision.MapsPoles,
            newRevision.ScreensDisease,
            newRevision.EffectiveFrom,
            newRevision.ReasonCode
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    policyRepository.AddRevision(newRevision);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "SurveyServiceRevision",
                        newRevision.Id,
                        "PUBLISH_REVISION",
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
            return Result.Failure<PublishSurveyServiceRevisionResponse>(
                BusinessPolicyError.ConcurrentUpdate());
        }
        catch (DbUpdateException ex)
        {
            return Result.Failure<PublishSurveyServiceRevisionResponse>(
                BusinessPolicyError.ConcurrentUpdate(ex.Message));
        }

        return Result.Success(new PublishSurveyServiceRevisionResponse(
            newRevision.Id,
            newRevision.SurveyServiceId,
            newRevision.VersionNumber,
            newRevision.PredecessorVersionId,
            newRevision.Name,
            newRevision.Description,
            newRevision.MapsPoles,
            newRevision.ScreensDisease,
            newRevision.EffectiveFrom,
            newRevision.EffectiveTo,
            newRevision.ReasonCode,
            newRevision.EvidenceReference));
    }
}
