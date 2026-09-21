using AgriDrone.Modules.Harvests.Application.Abstractions.Persistence;
using AgriDrone.Modules.Harvests.Application.Errors;
using AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriDrone.Modules.Harvests.Application.Features.VersionHarvestQualityGrade;

internal sealed class VersionHarvestQualityGradeHandler(
    IHarvestQualityGradeRepository repository,
    IHarvestsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        VersionHarvestQualityGradeCommand,
        Result<HarvestQualityGradeResponse>>
{
    private static readonly string[] VersionConstraints =
    [
        "uq_quality_grades_active_code",
        "uq_quality_grades_code_revision",
        "uq_quality_grades_supersedes"
    ];

    public async Task<Result<HarvestQualityGradeResponse>> Handle(
        VersionHarvestQualityGradeCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<HarvestQualityGradeResponse>(
                HarvestQualityGradeError.CurrentUserRequired());
        }

        var current = await repository.GetByIdAsync(
            request.GradeId,
            cancellationToken);

        if (current is null)
        {
            return Result.Failure<HarvestQualityGradeResponse>(
                HarvestQualityGradeError.NotFound());
        }

        if (!current.IsActive)
        {
            return Result.Failure<HarvestQualityGradeResponse>(
                HarvestQualityGradeError.AlreadyRetired());
        }

        if (current.Version != request.ExpectedVersion)
        {
            return Result.Failure<HarvestQualityGradeResponse>(
                HarvestQualityGradeError.ConcurrentUpdate());
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            current.Name,
            current.DisplayOrder,
            current.RevisionNumber,
            current.IsActive
        });

        repository.Update(current);

        var nextVersion = current.CreateNextVersion(
            request.Name,
            request.DisplayOrder,
            now);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            nextVersion.Name,
            nextVersion.DisplayOrder,
            nextVersion.RevisionNumber,
            nextVersion.SupersedesId,
            nextVersion.IsActive
        });

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    await unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);

                    repository.Add(nextVersion);

                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "HarvestQualityGrade",
                        nextVersion.Id,
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
            return Result.Failure<HarvestQualityGradeResponse>(
                HarvestQualityGradeError.ConcurrentUpdate());
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(VersionConstraints))
        {
            return Result.Failure<HarvestQualityGradeResponse>(
                HarvestQualityGradeError.ConcurrentUpdate());
        }

        return Result.Success(HarvestQualityGradeResponse.From(nextVersion));
    }
}
