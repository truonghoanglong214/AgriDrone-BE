using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Mapping;

public sealed class PlantChangeEvent : Entity
{
    private PlantChangeEvent()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid MissionId { get; private set; }

    public Guid? PlantId { get; private set; }

    public PlantChangeType ChangeType { get; private set; }

    public Point? ObservedLocation { get; private set; }

    public ReviewStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
