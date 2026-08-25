namespace AgriDrone.Modules.Identity.Domain.Roles;

internal interface IRoleRepository
{
    Task<Role?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> HasAssignedActiveUserAsync(
        string code,
        CancellationToken cancellationToken = default);
}
