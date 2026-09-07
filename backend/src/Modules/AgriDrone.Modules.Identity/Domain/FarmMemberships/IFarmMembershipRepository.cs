namespace AgriDrone.Modules.Identity.Domain.FarmMemberships;

public interface IFarmMembershipRepository
{
    void Add(FarmMembership membership);

    Task<FarmMembership?> GetByFarmAndUserAsync(
        Guid tenantId,
        Guid farmId,
        Guid userId,
        CancellationToken cancellationToken);
}
