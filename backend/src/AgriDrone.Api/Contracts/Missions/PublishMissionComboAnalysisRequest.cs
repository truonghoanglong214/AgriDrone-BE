namespace AgriDrone.Api.Contracts.Missions;

public sealed record PublishMissionComboAnalysisRequest(
    Guid JobId,
    int SourceRevision = 1);
