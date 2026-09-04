using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application.Features.Missions;

internal static class MissionResponseMapper
{
    public static MissionResponse Map(DroneMission mission)
    {
        return new MissionResponse(
            mission.Id,
            mission.TenantId,
            mission.FarmId,
            mission.ZoneId,
            mission.DroneId,
            mission.PilotUserId,
            mission.MissionCode,
            mission.MissionType,
            mission.Status,
            mission.ProcessingStatus,
            mission.ScheduledAt,
            mission.ScheduledEndAt,
            mission.StartedAt,
            mission.EndedAt,
            mission.SourceMapVersionId,
            mission.PublishedMapVersionId,
            mission.PreflightConfirmedBy,
            mission.PreflightConfirmedAt,
            mission.FlightParameters.RootElement.Clone(),
            mission.Notes,
            mission.Version,
            mission.CreatedAt,
            mission.UpdatedAt);
    }
}