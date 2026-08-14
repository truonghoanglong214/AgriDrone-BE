using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Domain.Farms;

public sealed class Farm : AggregateRoot
{
    private Farm()
    {
    }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Address { get; private set; }

    public Polygon? Boundary { get; private set; }

    public Point? CenterPoint { get; private set; }

    public decimal? AreaHectares { get; private set; }

    public GeneralStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }
}
