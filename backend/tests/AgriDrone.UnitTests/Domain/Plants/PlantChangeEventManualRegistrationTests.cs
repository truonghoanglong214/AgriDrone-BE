using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Plants;

public sealed class PlantChangeEventManualRegistrationTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ManualRegisteredWithoutGridCreatesConfirmedManualEvent()
    {
        var farmId = Guid.NewGuid();
        var plantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var location = CreatePoint();

        var changeEvent = PlantChangeEvent.ManualRegistered(
            farmId,
            plantId,
            location,
            rowIndex: null,
            columnIndex: null,
            actorId,
            " Found during field inspection. ",
            CreatedAt);

        Assert.NotEqual(Guid.Empty, changeEvent.Id);
        Assert.Equal(farmId, changeEvent.FarmId);
        Assert.Null(changeEvent.MissionId);
        Assert.Equal(plantId, changeEvent.PlantId);
        Assert.Equal(PlantChangeType.NewPlant, changeEvent.ChangeType);
        Assert.Equal(PlantChangeSource.Manual, changeEvent.Source);
        Assert.Null(changeEvent.OldLocation);
        Assert.Null(changeEvent.OldRowIndex);
        Assert.Null(changeEvent.OldColumnIndex);
        Assert.Null(changeEvent.NewRowIndex);
        Assert.Null(changeEvent.NewColumnIndex);
        Assert.Null(changeEvent.OldLifecycleStatus);
        Assert.Equal(
            PlantLifecycleStatus.Active,
            changeEvent.NewLifecycleStatus);
        Assert.Equal(actorId, changeEvent.CreatedBy);
        Assert.Equal(ReviewStatus.Confirmed, changeEvent.Status);
        Assert.Equal(
            "Found during field inspection.",
            changeEvent.Notes);
        Assert.Equal(actorId, changeEvent.ReviewedBy);
        Assert.Equal(CreatedAt, changeEvent.ReviewedAt);
        Assert.Equal(CreatedAt, changeEvent.CreatedAt);

        var storedLocation = Assert.IsType<Point>(changeEvent.NewLocation);
        Assert.NotSame(location, storedLocation);
        Assert.Equal(location.X, storedLocation.X);
        Assert.Equal(location.Y, storedLocation.Y);
        Assert.Equal(location.SRID, storedLocation.SRID);
    }

    [Fact]
    public void ManualRegisteredWithGridStoresCompleteGridPosition()
    {
        var changeEvent = CreateManualEvent(
            rowIndex: 3,
            columnIndex: 4);

        Assert.Equal(3, changeEvent.NewRowIndex);
        Assert.Equal(4, changeEvent.NewColumnIndex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ManualRegisteredRejectsMissingReason(string? reason)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManualEvent(reason: reason));

        Assert.Equal("reason", exception.ParamName);
    }

    [Theory]
    [InlineData(RequiredIdentifier.FarmId, "farmId")]
    [InlineData(RequiredIdentifier.PlantId, "plantId")]
    [InlineData(RequiredIdentifier.ActorId, "actorId")]
    public void ManualRegisteredRejectsEmptyRequiredIdentifier(
        RequiredIdentifier identifier,
        string expectedParameterName)
    {
        var farmId = Guid.NewGuid();
        var plantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        switch (identifier)
        {
            case RequiredIdentifier.FarmId:
                farmId = Guid.Empty;
                break;
            case RequiredIdentifier.PlantId:
                plantId = Guid.Empty;
                break;
            case RequiredIdentifier.ActorId:
                actorId = Guid.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(identifier));
        }

        var exception = Assert.Throws<ArgumentException>(() =>
            PlantChangeEvent.ManualRegistered(
                farmId,
                plantId,
                CreatePoint(),
                rowIndex: null,
                columnIndex: null,
                actorId,
                "Field inspection",
                CreatedAt));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(null, 1)]
    public void ManualRegisteredRejectsIncompleteGridPosition(
        int? rowIndex,
        int? columnIndex)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManualEvent(
                rowIndex: rowIndex,
                columnIndex: columnIndex));

        Assert.Equal("rowIndex", exception.ParamName);
    }

    [Theory]
    [InlineData(0, 1, "rowIndex")]
    [InlineData(-1, 1, "rowIndex")]
    [InlineData(1, 0, "columnIndex")]
    [InlineData(1, -1, "columnIndex")]
    public void ManualRegisteredRejectsNonPositiveGridPosition(
        int rowIndex,
        int columnIndex,
        string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateManualEvent(
                rowIndex: rowIndex,
                columnIndex: columnIndex));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void ManualRegisteredRejectsNullLocation()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreateManualEvent(useNullLocation: true));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void ManualRegisteredRejectsEmptyLocation()
    {
        var geometryFactory = new GeometryFactory(
            new PrecisionModel(),
            4326);
        var location = geometryFactory.CreatePoint((Coordinate)null!);

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManualEvent(location: location));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void ManualRegisteredRejectsUnexpectedLocationSrid()
    {
        var location = CreatePoint();
        location.SRID = 3857;

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManualEvent(location: location));

        Assert.Equal("location", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN, 10.0)]
    [InlineData(double.PositiveInfinity, 10.0)]
    [InlineData(106.0, double.NaN)]
    [InlineData(106.0, double.NegativeInfinity)]
    public void ManualRegisteredRejectsNonFiniteLocation(
        double longitude,
        double latitude)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManualEvent(
                location: CreatePoint(longitude, latitude)));

        Assert.Equal("location", exception.ParamName);
    }

    [Theory]
    [InlineData(-180.0001, 0.0)]
    [InlineData(180.0001, 0.0)]
    [InlineData(0.0, -90.0001)]
    [InlineData(0.0, 90.0001)]
    public void ManualRegisteredRejectsLocationOutsideBounds(
        double longitude,
        double latitude)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateManualEvent(
                location: CreatePoint(longitude, latitude)));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void ManualRegisteredRejectsDefaultTimestamp()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManualEvent(createdAt: default(DateTimeOffset)));

        Assert.Equal("createdAt", exception.ParamName);
    }

    [Fact]
    public void ManualRegisteredRejectsNonUtcTimestamp()
    {
        var nonUtcTimestamp =
            new DateTimeOffset(2026, 9, 20, 15, 0, 0, TimeSpan.FromHours(7));

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateManualEvent(createdAt: nonUtcTimestamp));

        Assert.Equal("createdAt", exception.ParamName);
    }

    [Fact]
    public void MappingCreatedRetainsMissionAiSemantics()
    {
        var farmId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var plantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var changeEvent = PlantChangeEvent.MappingCreated(
            farmId,
            missionId,
            plantId,
            CreatePoint(),
            rowIndex: 1,
            columnIndex: 2,
            actorId,
            CreatedAt);

        Assert.Equal(missionId, changeEvent.MissionId);
        Assert.Equal(PlantChangeType.NewPlant, changeEvent.ChangeType);
        Assert.Equal(PlantChangeSource.MissionAi, changeEvent.Source);
        Assert.Equal(1, changeEvent.NewRowIndex);
        Assert.Equal(2, changeEvent.NewColumnIndex);
        Assert.Null(changeEvent.Notes);
        Assert.Equal(ReviewStatus.Confirmed, changeEvent.Status);
    }

    [Fact]
    public void MappingPositionChangedRetainsPreviousState()
    {
        var oldLocation = CreatePoint(106.7, 10.7);
        var newLocation = CreatePoint(106.8, 10.8);

        var changeEvent = PlantChangeEvent.MappingPositionChanged(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            oldLocation,
            newLocation,
            oldRowIndex: 1,
            newRowIndex: 2,
            oldColumnIndex: 3,
            newColumnIndex: 4,
            PlantLifecycleStatus.Active,
            Guid.NewGuid(),
            CreatedAt);

        Assert.Equal(
            PlantChangeType.MappingDifference,
            changeEvent.ChangeType);
        Assert.Equal(PlantChangeSource.MissionAi, changeEvent.Source);
        Assert.Equal(1, changeEvent.OldRowIndex);
        Assert.Equal(2, changeEvent.NewRowIndex);
        Assert.Equal(3, changeEvent.OldColumnIndex);
        Assert.Equal(4, changeEvent.NewColumnIndex);
        Assert.Equal(
            PlantLifecycleStatus.Active,
            changeEvent.OldLifecycleStatus);
        Assert.NotSame(oldLocation, changeEvent.OldLocation);
        Assert.NotSame(newLocation, changeEvent.NewLocation);
    }

    private static PlantChangeEvent CreateManualEvent(
        Point? location = null,
        bool useNullLocation = false,
        int? rowIndex = null,
        int? columnIndex = null,
        string? reason = "Field inspection",
        DateTimeOffset? createdAt = null) =>
        PlantChangeEvent.ManualRegistered(
            Guid.NewGuid(),
            Guid.NewGuid(),
            useNullLocation ? null! : location ?? CreatePoint(),
            rowIndex,
            columnIndex,
            Guid.NewGuid(),
            reason!,
            createdAt ?? CreatedAt);

    private static Point CreatePoint(
        double longitude = 106.700981,
        double latitude = 10.776889) =>
        new(longitude, latitude)
        {
            SRID = 4326
        };

    public enum RequiredIdentifier
    {
        FarmId,
        PlantId,
        ActorId
    }
}
