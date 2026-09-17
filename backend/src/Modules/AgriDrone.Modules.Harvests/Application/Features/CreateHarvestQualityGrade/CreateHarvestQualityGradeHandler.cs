using AgriDrone.Modules.Harvests.Application.Abstractions.Persistence;
using AgriDrone.Modules.Harvests.Application.Errors;
using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Application.Features.CreateHarvestQualityGrade
{
    internal sealed class CreateHarvestQualityGradeHandler(
        IHarvestQualityGradeRepository repository,
        IHarvestsUnitOfWork unitOfWork,
        TimeProvider timeProvider) : IRequestHandler<CreateHarvestQualityGradeCommand, Result<HarvestQualityGradeResponse>>
    {
        private static readonly string[] CodeConstraints =
        [
            "uq_quality_grades_active_code",
            "uq_quality_grades_code_revision"
        ];

        public async Task<Result<HarvestQualityGradeResponse>> Handle(CreateHarvestQualityGradeCommand request, CancellationToken cancellationToken)
        {
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
