using System.Text.Json;
using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.RetireSourcedGuidance;

internal sealed class RetireSourcedGuidanceCommandHandler(
    ISourcedGuidanceRepository repository,
    IPlantsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RetireSourcedGuidanceCommand, Result>
{
    public async Task<Result> Handle(
        RetireSourcedGuidanceCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized || executionContext.ActorId is not Guid actorId)
            return Result.Failure(SourcedGuidanceError.Forbidden());

        var guidance = await repository.GetByIdAsync(command.GuidanceId, cancellationToken);
        if (guidance is null) return Result.Failure(SourcedGuidanceError.NotFound());
        if (guidance.Version != command.ExpectedVersion)
            return Result.Failure(SourcedGuidanceError.VersionConflict());

        var now = timeProvider.GetUtcNow();
        var previousStatus = guidance.Status;
        try
        {
            guidance.Retire(now, command.ExpectedVersion);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure(SourcedGuidanceError.InvalidTransition(exception.Message));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new { Status = previousStatus });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            guidance.Status,
            guidance.RetiredAt,
            guidance.EffectiveTo,
            guidance.Version
        });
        repository.Update(guidance);
        auditWriter.AddSystemAdminAction(unitOfWork, actorId, executionContext.CorrelationId,
            nameof(SourcedGuidance), guidance.Id, "RETIRE", oldData, newData, now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(SourcedGuidanceError.VersionConflict());
        }

        return Result.Success();
    }
}
