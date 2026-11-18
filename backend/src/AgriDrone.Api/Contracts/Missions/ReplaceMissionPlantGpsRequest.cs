namespace AgriDrone.Api.Contracts.Missions;

public sealed record ReplaceMissionPlantGpsRequest(
    IReadOnlyList<MissionPlantGpsItem> Trees,
    MappingProcessingCompletionRequest Processing,
    IReadOnlyList<MissionPlantGpsBoundaryPoint>? Boundary = null);

public sealed record MappingProcessingCompletionRequest(
    Guid ClientOperationId,
    Guid ModelVersionId,
    int SourceVersion,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    string AlgorithmVersion,
    DateTimeOffset StartedAt);

public sealed record MissionPlantGpsBoundaryPoint(
    decimal Longitude,
    decimal Latitude);

public sealed record MissionPlantGpsItem(string TreeId, int Row, int Col,
    decimal? Latitude, decimal? Longitude, decimal Confidence, string Status,
    string Source);
