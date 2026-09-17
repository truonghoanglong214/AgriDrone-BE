using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.VersionPlantCondition;

internal sealed class VersionPlantConditionHandler(
    IPlantConditionRepository plantConditionRepository,
    IPlantsUnitOfWork unitOfWork,
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

        plantConditionRepository.Update(current);

        var nextRevision = current.CreateNextRevision(
            request.Name,
            request.ScientificName,
            request.Description,
            timeProvider.GetUtcNow());

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    await unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);

                    plantConditionRepository.Add(nextRevision);

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
