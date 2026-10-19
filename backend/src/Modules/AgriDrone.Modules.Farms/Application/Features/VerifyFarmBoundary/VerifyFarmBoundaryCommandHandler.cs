using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.VerifyFarmBoundary;

internal sealed class VerifyFarmBoundaryCommandHandler(
    IFarmRepository farmRepository,
    IFarmBoundaryRepository boundaryRepository,
    IFarmUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<VerifyFarmBoundaryCommand, Result<VerifyFarmBoundaryResponse>>
{
    public async Task<Result<VerifyFarmBoundaryResponse>> Handle(
        VerifyFarmBoundaryCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<VerifyFarmBoundaryResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed || access.TenantId is not Guid tenantId)
        {
            return Result.Failure<VerifyFarmBoundaryResponse>(FarmError.NotFound());
        }

        var farm = await farmRepository.GetByIdAsync(
            tenantId,
            command.FarmId,
            cancellationToken);

        if (farm is null)
        {
            return Result.Failure<VerifyFarmBoundaryResponse>(FarmError.NotFound());
        }

        if (farm.Version != command.ExpectedFarmVersion)
        {
            return Result.Failure<VerifyFarmBoundaryResponse>(FarmError.ConcurrentUpdate());
        }

        var now = timeProvider.GetUtcNow();

        var currentBoundary = await boundaryRepository.GetCurrentApprovedAsync(
            tenantId,
            command.FarmId,
            cancellationToken);

        if (currentBoundary is null)
        {
            return Result.Failure<VerifyFarmBoundaryResponse>(
                FarmError.UnmappedFarmBoundaryRequiresCaptureSession());
        }

        if (!command.ExpectedCurrentBoundaryVersionNumber.HasValue ||
            currentBoundary.VersionNumber != command.ExpectedCurrentBoundaryVersionNumber.Value)
        {
            return Result.Failure<VerifyFarmBoundaryResponse>(
                FarmError.BoundaryConcurrentModification());
        }

        var nextVersionNumber = await boundaryRepository.GetNextVersionNumberAsync(
            tenantId,
            command.FarmId,
            cancellationToken);

        var newBoundary = FarmBoundary.CreateDraft(
            tenantId,
            command.FarmId,
            nextVersionNumber,
            command.Boundary,
            FarmBoundarySource.SystemManager,
            sourceSurveyRequestId: null,
            submittedBy: actorId,
            createdAt: now,
            evidenceReference: command.EvidenceReference);

        newBoundary.Approve(actorId, command.Reason, now);

        currentBoundary.Supersede(newBoundary.Id, now);
        boundaryRepository.Update(currentBoundary);

        farm.SetBoundary(newBoundary.Geometry, now);
        farmRepository.Update(farm);

        boundaryRepository.Add(newBoundary);

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            farmId = command.FarmId,
            boundaryId = newBoundary.Id,
            versionNumber = newBoundary.VersionNumber,
            supersededBoundaryId = currentBoundary?.Id,
            reason = command.Reason,
            evidenceReference = command.EvidenceReference
        });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: tenantId,
            farmId: farm.Id,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: "FarmBoundary",
            entityId: newBoundary.Id,
            action: "VERIFY_BOUNDARY",
            oldData: null,
            newData: auditData,
            createdAt: now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new VerifyFarmBoundaryResponse(
            newBoundary.Id,
            newBoundary.FarmId,
            newBoundary.VersionNumber,
            newBoundary.Geometry,
            newBoundary.Status.ToString(),
            newBoundary.Source.ToString(),
            newBoundary.EvidenceReference,
            actorId,
            now,
            command.Reason,
            newBoundary.Version));
    }
}
