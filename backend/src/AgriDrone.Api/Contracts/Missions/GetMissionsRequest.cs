using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record GetMissionsRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? ZoneId { get; init; }
    public Guid? DroneId { get; init; }
    public MissionType? MissionType { get; init; }
    public MissionStatus? Status { get; init; }
    public string? Search { get; init; }
}
