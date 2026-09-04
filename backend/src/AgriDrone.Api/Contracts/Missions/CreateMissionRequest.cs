using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record CreateMissionRequest(
    Guid ZoneId,
    Guid DroneId,
    Guid? PilotUserId,
    string MissionCode,
    MissionType MissionType,
    Guid? SourceMapVersionId,
    JsonElement? FlightParameters,
    string? Notes);