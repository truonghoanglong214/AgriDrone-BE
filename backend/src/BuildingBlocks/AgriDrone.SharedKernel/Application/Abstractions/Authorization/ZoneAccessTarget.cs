namespace AgriDrone.SharedKernel.Application.Abstractions.Authorization;

public sealed record ZoneAccessTarget(
    Guid TenantId,
    Guid FarmId,
    Guid ZoneId);
