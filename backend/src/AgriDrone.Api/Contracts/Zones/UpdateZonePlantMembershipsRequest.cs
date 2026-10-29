namespace AgriDrone.Api.Contracts.Zones;

public sealed record UpdateZonePlantMembershipsRequest(
    long ExpectedZoneVersion,
    string Reason,
    IReadOnlyList<ZonePlantMembershipOperationRequest> Operations);
