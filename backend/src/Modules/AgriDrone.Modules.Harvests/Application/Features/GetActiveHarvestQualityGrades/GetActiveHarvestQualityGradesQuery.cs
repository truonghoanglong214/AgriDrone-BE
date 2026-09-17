using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Harvests.Application.Features.GetActiveHarvestQualityGrades;

public sealed record GetActiveHarvestQualityGradesQuery
    : IRequest<Result<IReadOnlyList<HarvestQualityGradeCatalogResponse>>>;
