using AgriDrone.Modules.Harvests.Application.Features.GetActiveHarvestQualityGrades;

namespace AgriDrone.Modules.Harvests.Application.Abstractions.Queries;

internal interface IHarvestQualityGradeQueries
{
    Task<IReadOnlyList<HarvestQualityGradeCatalogResponse>> GetActiveAsync(
        CancellationToken cancellationToken = default);
}
