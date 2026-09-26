namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record AssignedFarmResponse(
    Guid TenantId,
    Guid FarmId,
    string Code,
    string Name,
    string? Address,
    decimal? AreaHectares,
    DateTimeOffset AssignedAt);
