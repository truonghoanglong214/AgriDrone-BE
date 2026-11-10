namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapAnalytics;

public sealed record BaseMapVersionMetricDto(
    Guid VersionId,
    int VersionNumber,
    string Status,
    DateTimeOffset? PublishedAt,
    int TotalPoles,
    int PolesDelta,
    double? DensityPolesPerHa,
    double PlantSpacingM,
    double RowSpacingM,
    double GridBearingDeg,
    int ManualChangesCount = 0);

public sealed record FarmBaseMapAnalyticsResponse(
    Guid FarmId,
    int TotalMappingVersions,
    DateTimeOffset? FirstMappedAt,
    DateTimeOffset? LatestMappedAt,
    double? FarmAreaHa,
    IReadOnlyList<BaseMapVersionMetricDto> Timeline);

