using AgriDrone.Modules.Harvests.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Harvests.Application.Features.GetActiveHarvestQualityGrades;

internal sealed class GetActiveHarvestQualityGradesQueryHandler(
    IHarvestQualityGradeQueries harvestQualityGradeQueries)
    : IRequestHandler<
        GetActiveHarvestQualityGradesQuery,
        Result<IReadOnlyList<HarvestQualityGradeCatalogResponse>>>
{
    public async Task<Result<IReadOnlyList<HarvestQualityGradeCatalogResponse>>> Handle(
        GetActiveHarvestQualityGradesQuery request,
        CancellationToken cancellationToken)
    {
        var grades = await harvestQualityGradeQueries.GetActiveAsync(cancellationToken);
        return Result.Success(grades);
    }
}
