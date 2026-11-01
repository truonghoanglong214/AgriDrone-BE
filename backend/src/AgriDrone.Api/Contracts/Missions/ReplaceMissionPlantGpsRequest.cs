namespace AgriDrone.Api.Contracts.Missions;

public sealed record ReplaceMissionPlantGpsRequest(
    IReadOnlyList<MissionPlantGpsItem> Trees,
    IReadOnlyList<MissionPlantGpsBoundaryPoint>? Boundary = null);

public sealed record MissionPlantGpsBoundaryPoint(
    decimal Longitude,
    decimal Latitude);

public sealed record MissionPlantGpsItem(string TreeId, int Row, int Col,
    decimal? Latitude, decimal? Longitude, decimal Confidence, string Status,
    string Source);
