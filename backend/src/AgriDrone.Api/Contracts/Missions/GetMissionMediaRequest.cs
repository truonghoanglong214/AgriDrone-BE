using AgriDrone.Modules.Missions.Domain.Media;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record GetMissionMediaRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public MediaType? MediaType { get; init; }
    public MissionMediaRole? MediaRole { get; init; }
}
