using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriDrone.Modules.Plants.Application.Features.RetirePlantCondition;

internal sealed class RetirePlantConditionHandler(
    IPlantConditionRepository plantConditionRepository,
    IPlantsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RetirePlantConditionCommand, Result>
{
    public async Task<Result> Handle(
        RetirePlantConditionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure(PlantConditionError.CurrentUserRequired());
        }

        var condition = await plantConditionRepository.GetByIdAsync(
            request.ConditionId,
            cancellationToken);

        if (condition is null)
        {
            return Result.Failure(PlantConditionError.NotFound());
        }

        if (!condition.IsActive)
        {
            return Result.Failure(PlantConditionError.AlreadyRetired());
        }

        if (condition.Version != request.ExpectedVersion)
        {
            return Result.Failure(PlantConditionError.ConcurrentUpdate());
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            condition.IsActive,
            condition.RetiredAt,
            condition.Version
        });

        plantConditionRepository.Update(condition);
        condition.Retire(now);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            condition.IsActive,
            condition.RetiredAt,
            condition.Version
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            "PlantCondition",
            condition.Id,
            "RETIRE",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(PlantConditionError.ConcurrentUpdate());
        }

        return Result.Success();
    }
}
