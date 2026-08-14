using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Plants;

public sealed class Plant : AggregateRoot
{
    private Plant()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid? ZoneId { get; private set; }

    public string PlantCode { get; private set; } = null!;

    public Point? Location { get; private set; }

    public Guid? CurrentMapVersionId { get; private set; }

    public int? RowIndex { get; private set; }

    public int? ColumnIndex { get; private set; }

    public decimal? LocationAccuracyM { get; private set; }

    public decimal? PositionConfidence { get; private set; }

    public PositionSource? PositionSource { get; private set; }

    public PlantLifecycleStatus LifecycleStatus { get; private set; }

    public Guid CurrentHealthLevelId { get; private set; }

    public DateTimeOffset? LastInspectedAt { get; private set; }

    public DateTimeOffset? MappedAt { get; private set; }

    public Guid? CreatedFromMissionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }
}
