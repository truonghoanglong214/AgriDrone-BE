namespace AgriDrone.Modules.Farms.Application.Abstractions.Zones;

public sealed record ZonePlantMembershipOperation(
    Guid PlantId,
    ZonePlantMembershipAction Action);
