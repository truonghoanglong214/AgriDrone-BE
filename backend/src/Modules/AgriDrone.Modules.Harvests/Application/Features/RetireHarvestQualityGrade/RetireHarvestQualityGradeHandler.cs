using AgriDrone.Modules.Harvests.Application.Abstractions.Persistence;
using AgriDrone.Modules.Harvests.Application.Errors;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Harvests.Application.Features.RetireHarvestQualityGrade;

internal sealed class RetireHarvestQualityGradeHandler(
    IHarvestQualityGradeRepository repository,
    IHarvestsUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<RetireHarvestQualityGradeCommand, Result>
{
    public async Task<Result> Handle(
        RetireHarvestQualityGradeCommand request,
        CancellationToken cancellationToken)
    {
        var grade = await repository.GetByIdAsync(
            request.GradeId,
            cancellationToken);

        if (grade is null)
        {
            return Result.Failure(HarvestQualityGradeError.NotFound());
        }

        if (!grade.IsActive)
        {
            return Result.Failure(
                HarvestQualityGradeError.AlreadyRetired());
        }

        if (grade.Version != request.ExpectedVersion)
        {
            return Result.Failure(
                HarvestQualityGradeError.ConcurrentUpdate());
        }

        repository.Update(grade);
        grade.Retire(timeProvider.GetUtcNow());

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                HarvestQualityGradeError.ConcurrentUpdate());
        }

        return Result.Success();
    }
}
