namespace AgriDrone.Api.Contracts.Missions;

public sealed record ProcessNodeOdmRequest(
    string? FeatureQuality = null,
    int? ResizeTo = null,
    string? PcQuality = null,
    int? MaxConcurrency = null,
    bool? FastOrthophoto = null);

public sealed record ProcessNodeOdmResponse(
    Guid MissionId,
    string TaskId,
    string Status,
    string Message);
