namespace AgriDrone.IntegrationContracts.Mapping;

/// <summary>Wire shape published to BE1 for the V3 baseline candidate event.</summary>
public sealed record BaselineMappingCandidatesReadyV3Message(
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    IReadOnlyList<BaselineMappingZoneV3Message> Zones);

public sealed record BaselineMappingZoneV3Message(
    Guid ZoneId,
    double RowSpacingM,
    double PlantSpacingM,
    double GridBearingDeg,
    IReadOnlyList<BaselineMappingCandidateV3Message> Candidates);

public sealed record BaselineMappingCandidateV3Message(
    double Longitude,
    double Latitude,
    int ProposedRowIndex,
    int ProposedColumnIndex,
    decimal PositionConfidence,
    string TreeId);
