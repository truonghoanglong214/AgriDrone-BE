using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.Tenants;

public sealed class Tenant : AggregateRoot
{
    private Tenant()
    {
    }

    private Tenant(Guid id,string code, string name, GeneralStatus status, DateTimeOffset createdAt)
    {
        Id = id;
        Code = code;
        Name = name;
        Status = status;
        CreatedAt = createdAt;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public GeneralStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public ICollection<TenantMembership> Memberships { get; private set; } = [];

    public static Tenant Create(string code, string name, GeneralStatus status, DateTimeOffset createdAt)
    {
        return new Tenant(
            Guid.NewGuid(),
            code,
            name,
            status,
            createdAt
            );
    }

    public void Activate(DateTimeOffset updateAt)
    {
        if (Status == GeneralStatus.Active)
            return;

        Status = GeneralStatus.Active;
        UpdatedAt = updateAt;
    }

    public void Deactivate(DateTimeOffset updateAt)
    {
        if (Status == GeneralStatus.Inactive)
            return;

        Status = GeneralStatus.Inactive;
        UpdatedAt = updateAt;
    }
}
