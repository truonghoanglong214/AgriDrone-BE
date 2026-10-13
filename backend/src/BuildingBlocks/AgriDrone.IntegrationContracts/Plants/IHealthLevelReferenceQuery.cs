namespace AgriDrone.IntegrationContracts.Plants;

/// <summary>
/// Provides stable cross-module references to system health levels.
/// </summary>
public interface IHealthLevelReferenceQuery
{
    /// <summary>
    /// Gets the identifier of the active UNKNOWN health level, or
    /// <see langword="null"/> when the required system definition is missing.
    /// </summary>
    Task<Guid?> GetActiveUnknownIdAsync(
        CancellationToken cancellationToken = default);
}
