namespace AgriDrone.SharedKernel.Domain;

public abstract class AggregateRoot : Entity;

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull;
