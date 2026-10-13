using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionUploadReadiness;

public sealed record MissionUploadReadinessResult(
    Guid MissionId,
    uint MissionVersion,
    MissionStatus Status,
    int RawImageCount,
    int RawVideoCount,
    int TelemetryPointCount,
    bool HasActiveUploadSessions,
    IReadOnlyList<AppError> Blockers)
{
    public bool CanFinalize => Blockers.Count == 0;
}