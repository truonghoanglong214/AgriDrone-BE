namespace AgriDrone.SharedKernel.Domain;

public abstract class Entity : Entity<Guid>;

public abstract class Entity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;
}
