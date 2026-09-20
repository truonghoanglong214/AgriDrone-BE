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

    private Plant(
        Guid id,
        Guid farmId,
        Guid zoneId,
        string plantCode,
        Point location,
        Guid? mapVersionId,
        int? rowIndex,
        int? columnIndex,
        decimal? locationAccuracyM,
        Guid healthLevelId,
        DateTimeOffset registeredAt)
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
        CurrentHealthLevelId = healthLevelId;
        LifecycleStatus = PlantLifecycleStatus.Active;
        PositionSource = Plants.PositionSource.Manual;
        MappedAt = mapVersionId.HasValue ? registeredAt : null;
        CreatedAt = registeredAt;
        UpdatedAt = registeredAt;
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
        var normalizedPlantCode = NormalizePlantCode(plantCode);
        EnsureLocation(location, nameof(location));
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
            normalizedPlantCode,
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

    public static Plant RegisterManually(
        Guid id,
        Guid farmId,
        Guid zoneId,
        string plantCode,
        Point location,
        Guid? mapVersionId,
        int? rowIndex,
        int? columnIndex,
        decimal? locationAccuracyM,
        Guid healthLevelId,
        DateTimeOffset registeredAt)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(zoneId);
        DomainGuard.NotEmpty(healthLevelId);
        var normalizedPlantCode = NormalizePlantCode(plantCode);
        EnsureLocation(location, nameof(location));
        EnsureOptionalGridPosition(mapVersionId, rowIndex, columnIndex);
        EnsureLocationAccuracy(locationAccuracyM);
        DomainGuard.Utc(registeredAt);

        return new Plant(
            id,
            farmId,
            zoneId,
            normalizedPlantCode,
            CopyPoint(location),
            mapVersionId,
            rowIndex,
            columnIndex,
            locationAccuracyM,
            healthLevelId,
            registeredAt);
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
        EnsureLocation(location, nameof(location));
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

        DomainGuard.Utc(updatedAt);

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
        EnsureLocationAccuracy(locationAccuracyM);

        if (positionConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(positionConfidence));
        }

        DomainGuard.Utc(mappedAt);
    }

    private static void EnsureLocationAccuracy(decimal? locationAccuracyM)
    {
        if (locationAccuracyM < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(locationAccuracyM));
        }

        if (locationAccuracyM > 99_999.999m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(locationAccuracyM),
                "Location accuracy cannot exceed 99,999.999.");
        }
    }

    private static void EnsureOptionalGridPosition(
        Guid? mapVersionId,
        int? rowIndex,
        int? columnIndex)
    {
        var hasMapVersion = mapVersionId.HasValue;
        var hasRowIndex = rowIndex.HasValue;
        var hasColumnIndex = columnIndex.HasValue;

        if (hasMapVersion != hasRowIndex || hasMapVersion != hasColumnIndex)
        {
            throw new ArgumentException(
                "Map version, row index, and column index must either all be provided or all be omitted.",
                nameof(mapVersionId));
        }

        if (!hasMapVersion)
        {
            return;
        }

        DomainGuard.NotEmpty(mapVersionId!.Value, nameof(mapVersionId));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            rowIndex!.Value,
            1,
            nameof(rowIndex));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            columnIndex!.Value,
            1,
            nameof(columnIndex));
    }

    private static string NormalizePlantCode(string plantCode)
    {
        if (string.IsNullOrWhiteSpace(plantCode))
        {
            throw new ArgumentException(
                "Plant code cannot be null, empty, or whitespace.",
                nameof(plantCode));
        }

        var normalizedPlantCode = plantCode.Trim().ToUpperInvariant();
        if (normalizedPlantCode.Length > 50)
        {
            throw new ArgumentException(
                "Plant code cannot exceed 50 characters.",
                nameof(plantCode));
        }

        return normalizedPlantCode;
    }

    private static Point CopyPoint(Point point) =>
        new(point.X, point.Y)
        {
            SRID = point.SRID
        };

    private static void EnsureLocation(
        Point location,
        string parameterName)
    {
        if (location is null)
            throw new ArgumentNullException(parameterName);

        if (location.IsEmpty)
            throw new ArgumentException(
                "Location cannot be empty.",
                parameterName);

        if (location.SRID != 4326)
            throw new ArgumentException(
                "Location must have SRID 4326.",
                parameterName);

        if (!double.IsFinite(location.X) || !double.IsFinite(location.Y))
            throw new ArgumentException(
                "Location coordinates must be finite numbers.",
                parameterName);

        if (location.X < -180 || location.X > 180)
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Longitude must be between -180 and 180 degrees.");

        if (location.Y < -90 || location.Y > 90)
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Latitude must be between -90 and 90 degrees.");
    }

}
