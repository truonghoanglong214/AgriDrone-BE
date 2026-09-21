using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriDrone.Modules.Plants.Application.Features.VersionPlantCondition;

internal sealed class VersionPlantConditionHandler(
    IPlantConditionRepository plantConditionRepository,
    IPlantsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<VersionPlantConditionCommand, Result<PlantConditionResponse>>
{
    private static readonly string[] VersionConstraints =
    [
        "uq_plant_conditions_active_code",
        "uq_plant_conditions_code_revision",
        "uq_plant_conditions_supersedes"
    ];

    public async Task<Result<PlantConditionResponse>> Handle(
        VersionPlantConditionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.CurrentUserRequired());
        }

        var current = await plantConditionRepository.GetByIdAsync(
            request.ConditionId,
            cancellationToken);

        if (current is null)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.NotFound());
        }

        if (!current.IsActive)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.AlreadyRetired());
        }

        if (current.Version != request.ExpectedVersion)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.ConcurrentUpdate());
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            current.Name,
            current.ScientificName,
            current.Description,
            current.RevisionNumber,
            current.IsActive
        });

        plantConditionRepository.Update(current);

        var nextRevision = current.CreateNextRevision(
            request.Name,
            request.ScientificName,
            request.Description,
            now);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            nextRevision.Name,
            nextRevision.ScientificName,
            nextRevision.Description,
            nextRevision.RevisionNumber,
            nextRevision.SupersedesId,
            nextRevision.IsActive
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    await unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);

                    plantConditionRepository.Add(nextRevision);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "PlantCondition",
                        nextRevision.Id,
                        "VERSION",
                        oldData,
                        newData,
                        now);

                    await unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);

                    return true;
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.ConcurrentUpdate());
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(VersionConstraints))
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.ConcurrentUpdate());
        }

        return Result.Success(PlantConditionResponse.From(nextRevision));
    }
}
