namespace AgriDrone.Modules.Farms.Application.Abstractions.Zones;

public sealed record ZonePlantMembershipChangeResult(
    int AssignedCount,
    int UnassignedCount);
