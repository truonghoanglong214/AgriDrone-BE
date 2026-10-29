namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionPlantGpsPublishCandidate(
    string TreeId,
    int Row,
    int Col,
    decimal Latitude,
    decimal Longitude,
    decimal Confidence,
    string Status,
    string Source);
