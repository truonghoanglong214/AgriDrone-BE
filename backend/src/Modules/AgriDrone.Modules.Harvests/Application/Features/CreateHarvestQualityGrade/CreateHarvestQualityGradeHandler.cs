using AgriDrone.Modules.Harvests.Application.Abstractions.Persistence;
using AgriDrone.Modules.Harvests.Application.Errors;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade
{
    internal sealed class CreateHarvestQualityGradeHandler(
        IHarvestQualityGradeRepository repository,
        IHarvestsUnitOfWork unitOfWork,
        IAuditWriter auditWriter,
        IExecutionContext executionContext,
        TimeProvider timeProvider) : IRequestHandler<CreateHarvestQualityGradeCommand, Result<HarvestQualityGradeResponse>>
    {
        private static readonly string[] CodeConstraints =
        [
            "uq_quality_grades_active_code",
            "uq_quality_grades_code_revision"
        ];

        public async Task<Result<HarvestQualityGradeResponse>> Handle(CreateHarvestQualityGradeCommand request, CancellationToken cancellationToken)
        {
            if (executionContext.ActorId is not Guid actorId)
                return Result.Failure<HarvestQualityGradeResponse>(HarvestQualityGradeError.CurrentUserRequired());

            var normalizedCode = request.Code.Trim().ToUpperInvariant();

            var existingCode = await repository.CodeExistsAsync(
                normalizedCode,
                cancellationToken);
            if (existingCode)
                return Result.Failure<HarvestQualityGradeResponse>(HarvestQualityGradeError.CodeAlreadyExists(normalizedCode));

            var now = timeProvider.GetUtcNow();
            var grade = HarvestQualityGrade.Create(
                normalizedCode,
                request.Name.Trim(),
                request.DisplayOrder,
                now);

            repository.Add(grade);

            using var newData = JsonSerializer.SerializeToDocument(new
            {
                grade.Code,
                grade.Name,
                grade.DisplayOrder,
                grade.RevisionNumber,
                grade.IsActive
            });

            auditWriter.AddSystemAdminAction(
                unitOfWork,
                actorId,
                executionContext.CorrelationId,
                "HarvestQualityGrade",
                grade.Id,
                "CREATE",
                oldData: null,
                newData,
                now);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);

            }
            catch (DbUpdateException exception)
            when (exception.IsUniqueConstraintViolation(CodeConstraints))
            {
                return Result.Failure<HarvestQualityGradeResponse>(
                    HarvestQualityGradeError.CodeAlreadyExists(normalizedCode));
            }

            return Result.Success(HarvestQualityGradeResponse.From(grade));
        }
    }
}
