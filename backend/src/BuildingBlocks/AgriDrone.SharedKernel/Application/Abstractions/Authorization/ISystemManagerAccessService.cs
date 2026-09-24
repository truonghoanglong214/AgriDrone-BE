namespace AgriDrone.SharedKernel.Application.Abstractions.Authorization;

public interface ISystemManagerAccessService
{
    Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(
        Guid farmId,
        CancellationToken cancellationToken = default);
}

public sealed record SystemManagerFarmAccess(
    bool IsAllowed,
    Guid? TenantId,
    Guid? FarmId,
    Guid? ManagerProfileId,
    string? DenialReason)
{
    public static SystemManagerFarmAccess Allowed(
        Guid tenantId,
        Guid farmId,
        Guid managerProfileId) =>
        new(true, tenantId, farmId, managerProfileId, null);

    public static SystemManagerFarmAccess Denied(string reason) =>
        new(false, null, null, null, reason);
}
