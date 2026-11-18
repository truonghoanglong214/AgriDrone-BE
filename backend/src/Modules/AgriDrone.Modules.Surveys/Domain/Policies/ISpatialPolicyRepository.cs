namespace AgriDrone.Modules.Surveys.Domain;

public interface ISpatialPolicyRepository
{
    Task<SpatialPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SpatialPolicy?> GetEffectiveAsync(
        string streamKey,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    void Add(SpatialPolicy policy);
}
