using AgriDrone.Modules.Harvests.Application.Abstractions.Queries;
using AgriDrone.Modules.Harvests.Application.Features.GetActiveHarvestQualityGrades;
using AgriDrone.Modules.Harvests.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Harvests.Infrastructure.Queries;

internal sealed class HarvestQualityGradeQueries(
    HarvestsDbContext context) : IHarvestQualityGradeQueries
{
    public async Task<IReadOnlyList<HarvestQualityGradeCatalogResponse>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.HarvestQualityGrades
            .AsNoTracking()
            .Where(grade => grade.IsActive)
            .OrderBy(grade => grade.DisplayOrder)
            .ThenBy(grade => grade.Code)
            .Select(grade => new HarvestQualityGradeCatalogResponse(
                grade.Id,
                grade.Code,
                grade.Name,
                grade.DisplayOrder,
                grade.RevisionNumber))
            .ToListAsync(cancellationToken);
    }
}
