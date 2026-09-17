using AgriDrone.Modules.Harvests.Domain.Quality;
using AgriDrone.Modules.Harvests.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Infrastructure.Repositories
{
    internal sealed class HarvestQualityGradeRepository(
        HarvestsDbContext context) : IHarvestQualityGradeRepository
    {
        public void Add(HarvestQualityGrade grade)
        {
            ArgumentNullException.ThrowIfNull(grade, nameof(grade));
            context.HarvestQualityGrades.Add(grade);
        }

        public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            return context.HarvestQualityGrades
                .AsNoTracking()
                .AnyAsync(q => q.Code == normalizedCode, cancellationToken);
        }

        public Task<HarvestQualityGrade?> GetByIdAsync(Guid gradeId, CancellationToken cancellationToken = default)
        {
            return context.HarvestQualityGrades
                .AsNoTracking()
                .SingleOrDefaultAsync(q => q.Id == gradeId, cancellationToken);
        }

        public void Update(HarvestQualityGrade grade)
        {
            context.HarvestQualityGrades.Update(grade);
        }
    }
}
