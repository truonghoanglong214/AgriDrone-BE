using AgriDrone.SharedKernel.Domain;
using AgriDrone.Modules.Plants.Domain.Plants;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Mapping;

public sealed class PlantChangeEvent : Entity
{
    private PlantChangeEvent()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid? MissionId { get; private set; }

    public Guid? PlantId { get; private set; }

    public PlantChangeType ChangeType { get; private set; }

    public PlantChangeSource Source { get; private set; }

    public Point? OldLocation { get; private set; }

    public Point? NewLocation { get; private set; }

    public int? OldRowIndex { get; private set; }

    public int? NewRowIndex { get; private set; }

    public int? OldColumnIndex { get; private set; }

    public int? NewColumnIndex { get; private set; }

    public PlantLifecycleStatus? OldLifecycleStatus { get; private set; }

    public PlantLifecycleStatus? NewLifecycleStatus { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public ReviewStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Plant? Plant { get; private set; }
}
