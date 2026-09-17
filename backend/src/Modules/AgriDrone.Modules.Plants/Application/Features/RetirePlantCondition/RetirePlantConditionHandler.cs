using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.RetirePlantCondition;

internal sealed class RetirePlantConditionHandler(
    IPlantConditionRepository plantConditionRepository,
    IPlantsUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<RetirePlantConditionCommand, Result>
{
    public async Task<Result> Handle(
        RetirePlantConditionCommand request,
        CancellationToken cancellationToken)
    {
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

        plantConditionRepository.Update(condition);
        condition.Retire(timeProvider.GetUtcNow());

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
