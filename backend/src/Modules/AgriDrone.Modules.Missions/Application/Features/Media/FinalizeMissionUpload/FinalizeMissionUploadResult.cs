using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.FinalizeMissionUpload;

public sealed record FinalizeMissionUploadResult(
    Guid MissionId,
    MissionStatus Status,
    int MediaCount,
    int TelemetryPointCount,
    uint MissionVersion);