using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.CreateMission;

public sealed record CreateMissionCommand(
    Guid TenantId,
    Guid FarmId,
    Guid ZoneId,
    Guid DroneId,
    Guid? PilotUserId,
    string MissionCode,
    MissionType MissionType,
    Guid? SourceMapVersionId,
    JsonElement? FlightParameters,
    string? Notes)
    : IRequest<Result<MissionResponse>>;