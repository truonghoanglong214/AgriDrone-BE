namespace AgriDrone.IntegrationContracts.Identity;

public interface ITenantOwnerRequestReferenceQuery
{
    Task<TenantOwnerRequestReference?> GetActiveOwnerAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record TenantOwnerRequestReference(
    Guid TenantId,
    Guid UserId,
    string FullName,
    string Email,
    string? Phone);
