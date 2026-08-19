namespace AgriDrone.SharedKernel.Application.Abstractions.Authorization;

public sealed record FarmAccessTarget(Guid TenantId, Guid FarmId);
