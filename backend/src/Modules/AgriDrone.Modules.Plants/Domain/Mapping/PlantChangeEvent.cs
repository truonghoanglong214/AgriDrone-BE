using AgriDrone.SharedKernel.Domain;
using AgriDrone.Modules.Plants.Domain.Plants;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Mapping;

public sealed class PlantChangeEvent : Entity
{
    private PlantChangeEvent()
    {
    }

    private PlantChangeEvent(
        Guid id,
        Guid farmId,
        Guid missionId,
        Guid plantId,
        PlantChangeType changeType,
        Point? oldLocation,
        Point newLocation,
        int? oldRowIndex,
        int newRowIndex,
        int? oldColumnIndex,
        int newColumnIndex,
        PlantLifecycleStatus? oldLifecycleStatus,
        Guid actorId,
        DateTimeOffset createdAt)
    {
        Id = id;
        FarmId = farmId;
        MissionId = missionId;
        PlantId = plantId;
        ChangeType = changeType;
        Source = PlantChangeSource.MissionAi;
        OldLocation = CopyPoint(oldLocation);
        NewLocation = CopyPoint(newLocation);
        OldRowIndex = oldRowIndex;
        NewRowIndex = newRowIndex;
        OldColumnIndex = oldColumnIndex;
        NewColumnIndex = newColumnIndex;
        OldLifecycleStatus = oldLifecycleStatus;
        NewLifecycleStatus = PlantLifecycleStatus.Active;
        CreatedBy = actorId;
        Status = ReviewStatus.Confirmed;
        ReviewedBy = actorId;
        ReviewedAt = createdAt;
        CreatedAt = createdAt;
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

    public static PlantChangeEvent MappingCreated(
        Guid farmId,
        Guid missionId,
        Guid plantId,
        Point location,
        int rowIndex,
        int columnIndex,
        Guid actorId,
        DateTimeOffset createdAt) =>
        Create(
            farmId,
            missionId,
            plantId,
            PlantChangeType.NewPlant,
            oldLocation: null,
            location,
            oldRowIndex: null,
            rowIndex,
            oldColumnIndex: null,
            columnIndex,
            oldLifecycleStatus: null,
            actorId,
            createdAt);

    public static PlantChangeEvent MappingPositionChanged(
        Guid farmId,
        Guid missionId,
        Guid plantId,
        Point? oldLocation,
        Point newLocation,
        int? oldRowIndex,
        int newRowIndex,
        int? oldColumnIndex,
        int newColumnIndex,
        PlantLifecycleStatus lifecycleStatus,
        Guid actorId,
        DateTimeOffset createdAt) =>
        Create(
            farmId,
            missionId,
            plantId,
            PlantChangeType.MappingDifference,
            oldLocation,
            newLocation,
            oldRowIndex,
            newRowIndex,
            oldColumnIndex,
            newColumnIndex,
            lifecycleStatus,
            actorId,
            createdAt);

    private static PlantChangeEvent Create(
        Guid farmId,
        Guid missionId,
        Guid plantId,
        PlantChangeType changeType,
        Point? oldLocation,
        Point newLocation,
        int? oldRowIndex,
        int newRowIndex,
        int? oldColumnIndex,
        int newColumnIndex,
        PlantLifecycleStatus? oldLifecycleStatus,
        Guid actorId,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(farmId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(missionId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(plantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(newLocation);
        ArgumentOutOfRangeException.ThrowIfLessThan(newRowIndex, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(newColumnIndex, 1);

        if (createdAt == default || createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "CreatedAt must be a non-default UTC timestamp.",
                nameof(createdAt));
        }

        return new PlantChangeEvent(
            Guid.NewGuid(),
            farmId,
            missionId,
            plantId,
            changeType,
            oldLocation,
            newLocation,
            oldRowIndex,
            newRowIndex,
            oldColumnIndex,
            newColumnIndex,
            oldLifecycleStatus,
            actorId,
            createdAt);
    }

    private static Point? CopyPoint(Point? point) =>
        point is null
            ? null
            : new Point(point.X, point.Y)
            {
                SRID = point.SRID
            };
}
