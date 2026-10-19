using System.Text.Json;
using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.Modules.Plants.Application.Features.Guidance.CreateSourcedGuidance;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.ActivateSourcedGuidance;

internal sealed class ActivateSourcedGuidanceCommandHandler(
    ISourcedGuidanceRepository repository,
    IPlantsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ActivateSourcedGuidanceCommand,
        Result<SourcedGuidanceResponse>>
{
    public async Task<Result<SourcedGuidanceResponse>> Handle(
        ActivateSourcedGuidanceCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId)
            return Result.Failure<SourcedGuidanceResponse>(SourcedGuidanceError.Forbidden());

        var guidance = await repository.GetByIdAsync(command.GuidanceId, cancellationToken);
        if (guidance is null)
            return Result.Failure<SourcedGuidanceResponse>(SourcedGuidanceError.NotFound());

        if (guidance.Version != command.ExpectedVersion)
            return Result.Failure<SourcedGuidanceResponse>(SourcedGuidanceError.VersionConflict());

        if (await repository.HasEffectiveOverlapAsync(
                guidance.PlantConditionId!.Value,
                guidance.EffectiveFrom,
                guidance.EffectiveTo,
                guidance.Id,
                cancellationToken))
            return Result.Failure<SourcedGuidanceResponse>(SourcedGuidanceError.EffectiveWindowOverlap());

        var now = timeProvider.GetUtcNow();
        var previousStatus = guidance.Status;
        try
        {
            guidance.Activate(now, command.ExpectedVersion);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<SourcedGuidanceResponse>(
                SourcedGuidanceError.InvalidTransition(exception.Message));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new { Status = previousStatus });
        using var newData = JsonSerializer.SerializeToDocument(new { guidance.Status, guidance.Version });
        repository.Update(guidance);
        auditWriter.AddSystemAdminAction(unitOfWork, actorId, executionContext.CorrelationId,
            nameof(SourcedGuidance), guidance.Id, "ACTIVATE", oldData, newData, now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SourcedGuidanceResponse>(SourcedGuidanceError.VersionConflict());
        }

        return Result.Success(CreateSourcedGuidanceCommandHandler.Map(guidance));
    }
}
