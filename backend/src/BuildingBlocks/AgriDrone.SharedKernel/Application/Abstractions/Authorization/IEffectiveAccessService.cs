namespace AgriDrone.SharedKernel.Application.Abstractions.Authorization;

public interface IEffectiveAccessService
{
    Task<AccessDecision> CheckTenantAsync(
        Guid actorId,
        Guid tenantId,
        TenantAccessLevel requiredAccess,
        CancellationToken cancellationToken = default);

    Task<AccessDecision> CheckFarmAsync(
        Guid actorId,
        Guid tenantId,
        Guid farmId,
        FarmAccessLevel requiredAccess,
        CancellationToken cancellationToken = default);

    Task<AccessDecision> CheckZoneAsync(
        Guid actorId,
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        FarmAccessLevel requiredAccess,
        CancellationToken cancellationToken = default);
}
