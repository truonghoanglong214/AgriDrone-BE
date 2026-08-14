using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.Tenants;

public sealed class Tenant : AggregateRoot
{
    private Tenant()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public GeneralStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }
}
