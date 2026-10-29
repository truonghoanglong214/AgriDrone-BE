namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public sealed record MissionPlantGpsCandidateInput(
    string TreeId,
    int Row,
    int Col,
    decimal Latitude,
    decimal Longitude,
    decimal Confidence,
    string Status,
    string Source);
