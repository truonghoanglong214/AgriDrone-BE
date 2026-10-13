namespace AgriDrone.Modules.Identity.Domain.SystemManagers;

public interface ISystemManagerProfileRepository
{
    Task<SystemManagerProfile?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<SystemManagerProfile?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    void Add(SystemManagerProfile profile);
}

public interface IFarmManagerAssignmentRepository
{
    Task<FarmManagerAssignment?> GetActiveByFarmIdAsync(
        Guid farmId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FarmManagerAssignment>> GetActiveByProfileIdAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    void Add(FarmManagerAssignment assignment);
}
