namespace AgriDrone.IntegrationContracts.Farms;

public interface IFarmAssignmentReferenceQuery
{
    Task<bool> IsActiveFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);
}
