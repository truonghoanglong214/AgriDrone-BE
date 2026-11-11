using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application.Features.Media.ResetMissionUpload;

public sealed record ResetMissionUploadResult(
    Guid MissionId,
    MissionStatus Status,
    int DeletedMediaCount,
    bool TelemetryReset,
    uint MissionVersion);

