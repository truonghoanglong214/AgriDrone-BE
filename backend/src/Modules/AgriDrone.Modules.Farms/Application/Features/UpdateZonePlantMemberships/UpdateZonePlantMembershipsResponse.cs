namespace AgriDrone.Modules.Farms.Application.Features.UpdateZonePlantMemberships;

public sealed record UpdateZonePlantMembershipsResponse(
    Guid ZoneId,
    int AssignedCount,
    int UnassignedCount);
