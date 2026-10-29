namespace AgriDrone.Api.Contracts.Zones;

public sealed record ZonePlantMembershipOperationRequest(
    Guid PlantId,
    string Action);
