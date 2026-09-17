using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Domain.Quality
{
    internal interface IHarvestQualityGradeRepository
    {
        Task<HarvestQualityGrade?> GetByIdAsync(
            Guid gradeId,
            CancellationToken cancellationToken = default);

        Task<bool> CodeExistsAsync(
            string code,
            CancellationToken cancellationToken = default);

        void Add(HarvestQualityGrade grade);

        void Update(HarvestQualityGrade grade);
    }
}
