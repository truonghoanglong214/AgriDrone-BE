using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Plants;

public sealed class Plant : AggregateRoot
{
    private Plant()
    {
    }

    private Plant(
        Guid id,
        Guid farmId,
        Guid zoneId,
        string plantCode,
        Point location,
        Guid mapVersionId,
        int rowIndex,
        int columnIndex,
        decimal? locationAccuracyM,
        decimal positionConfidence,
        Guid healthLevelId,
        Guid sourceMissionId,
        DateTimeOffset mappedAt)
    {
        Id = id;
        FarmId = farmId;
        ZoneId = zoneId;
        PlantCode = plantCode;
        Location = location;
        CurrentMapVersionId = mapVersionId;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        LocationAccuracyM = locationAccuracyM;
        PositionConfidence = positionConfidence;
        PositionSource = Plants.PositionSource.MappingAi;
        LifecycleStatus = PlantLifecycleStatus.Active;
        CurrentHealthLevelId = healthLevelId;
        MappedAt = mappedAt;
        CreatedFromMissionId = sourceMissionId;
        CreatedAt = mappedAt;
        UpdatedAt = mappedAt;
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

    public HealthLevel CurrentHealthLevel { get; private set; } = null!;

    public ICollection<PlantScan> Scans { get; private set; } = [];

    public ICollection<PlantChangeEvent> ChangeEvents { get; private set; } = [];

    public static Plant CreateFromMapping(
        Guid id,
        Guid farmId,
        Guid zoneId,
        string plantCode,
        Point location,
        Guid mapVersionId,
        int rowIndex,
        int columnIndex,
        decimal? locationAccuracyM,
        decimal positionConfidence,
        Guid healthLevelId,
        Guid sourceMissionId,
        DateTimeOffset mappedAt)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(zoneId);
        DomainGuard.NotEmpty(mapVersionId);
        DomainGuard.NotEmpty(healthLevelId);
        DomainGuard.NotEmpty(sourceMissionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(plantCode);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowIndex, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, 1);

        EnsureMappingMeasurements(
            locationAccuracyM,
            positionConfidence,
            mappedAt);

        return new Plant(
            id,
            farmId,
            zoneId,
            plantCode.Trim(),
            CopyPoint(location),
            mapVersionId,
            rowIndex,
            columnIndex,
            locationAccuracyM,
            positionConfidence,
            healthLevelId,
            sourceMissionId,
            mappedAt);
    }

    public void ApplyPublishedMapPosition(
        Guid mapVersionId,
        Point location,
        int rowIndex,
        int columnIndex,
        decimal? locationAccuracyM,
        decimal positionConfidence,
        DateTimeOffset mappedAt)
    {
        DomainGuard.NotEmpty(mapVersionId);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowIndex, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, 1);
        EnsureMappingMeasurements(
            locationAccuracyM,
            positionConfidence,
            mappedAt);

        if (LifecycleStatus != PlantLifecycleStatus.Active)
        {
            throw new InvalidOperationException(
                $"Plant '{Id}' in lifecycle '{LifecycleStatus}' cannot be assigned to a published map.");
        }

        CurrentMapVersionId = mapVersionId;
        Location = CopyPoint(location);
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        LocationAccuracyM = locationAccuracyM;
        PositionConfidence = positionConfidence;
        PositionSource = Plants.PositionSource.MappingAi;
        MappedAt = mappedAt;
        UpdatedAt = mappedAt;
    }

    public void ClearGridPositionForRemap(DateTimeOffset updatedAt)
    {
        if (LifecycleStatus != PlantLifecycleStatus.Active)
        {
            throw new InvalidOperationException(
                $"Plant '{Id}' in lifecycle '{LifecycleStatus}' cannot be remapped.");
        }

        if (updatedAt == default || updatedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "UpdatedAt must be a non-default UTC timestamp.",
                nameof(updatedAt));
        }

        CurrentMapVersionId = null;
        RowIndex = null;
        ColumnIndex = null;
        UpdatedAt = updatedAt;
    }

    private static void EnsureMappingMeasurements(
        decimal? locationAccuracyM,
        decimal positionConfidence,
        DateTimeOffset mappedAt)
    {
        if (locationAccuracyM < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(locationAccuracyM));
        }

        if (positionConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(positionConfidence));
        }

        if (mappedAt == default || mappedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "MappedAt must be a non-default UTC timestamp.",
                nameof(mappedAt));
        }
    }

    private static Point CopyPoint(Point point) =>
        new(point.X, point.Y)
        {
            SRID = point.SRID
        };

}
