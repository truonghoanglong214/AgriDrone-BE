using AgriDrone.Modules.Harvests.Application.Abstractions.Persistence;
using AgriDrone.Modules.Harvests.Application.Errors;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriDrone.Modules.Harvests.Application.Features.RetireHarvestQualityGrade;

internal sealed class RetireHarvestQualityGradeHandler(
    IHarvestQualityGradeRepository repository,
    IHarvestsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RetireHarvestQualityGradeCommand, Result>
{
    public async Task<Result> Handle(
        RetireHarvestQualityGradeCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure(HarvestQualityGradeError.CurrentUserRequired());
        }

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

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            grade.IsActive,
            grade.RetiredAt,
            grade.Version
        });

        repository.Update(grade);
        grade.Retire(now);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            grade.IsActive,
            grade.RetiredAt,
            grade.Version
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            "HarvestQualityGrade",
            grade.Id,
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
            return Result.Failure(
                HarvestQualityGradeError.ConcurrentUpdate());
        }

        return Result.Success();
    }
}
