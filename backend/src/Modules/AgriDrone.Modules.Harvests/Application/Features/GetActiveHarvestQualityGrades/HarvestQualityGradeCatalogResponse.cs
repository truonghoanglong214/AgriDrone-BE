namespace AgriDrone.Modules.Harvests.Application.Features.GetActiveHarvestQualityGrades;

public sealed record HarvestQualityGradeCatalogResponse(
    Guid Id,
    string Code,
    string Name,
    int DisplayOrder,
    int RevisionNumber);
