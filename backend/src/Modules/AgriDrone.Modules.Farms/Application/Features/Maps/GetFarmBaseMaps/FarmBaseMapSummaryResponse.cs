namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMaps;

public sealed record FarmBaseMapSummaryResponse(
    Guid VersionId,
    Guid FarmId,
    int VersionNumber,
    string Status,
    Guid? SourceMissionId,
    string? SourceMissionCode,
    DateTimeOffset? FlightDate,
    Guid? SourceSurveyOrderId,
    Guid? FarmBoundaryVersionId,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    int TotalPoles,
    string? PreviewUrl,
    DateTimeOffset? PublishedAt,
    Guid? PublishedBy,
    DateTimeOffset CreatedAt);

