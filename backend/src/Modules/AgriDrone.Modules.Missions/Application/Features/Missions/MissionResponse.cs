using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application.Features.Missions;

public sealed record MissionResponse(
    Guid Id,
    Guid TenantId,
    Guid FarmId,
    Guid ZoneId,
    Guid DroneId,
    Guid? PilotUserId,
    string MissionCode,
    MissionType MissionType,
    MissionStatus Status,
    ProcessingStatus ProcessingStatus,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? ScheduledEndAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    Guid? SourceMapVersionId,
    Guid? PublishedMapVersionId,
    Guid? PreflightConfirmedBy,
    DateTimeOffset? PreflightConfirmedAt,
    JsonElement FlightParameters,
    string? Notes,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);