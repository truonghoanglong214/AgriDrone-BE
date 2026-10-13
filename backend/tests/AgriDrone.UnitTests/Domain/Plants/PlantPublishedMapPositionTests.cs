using AgriDrone.Modules.Plants.Domain.Plants;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Plants;

public sealed class PlantPublishedMapPositionTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset RemappedAt =
        CreatedAt.AddHours(2);

    [Fact]
    public void ApplyPublishedMapPositionUpdatesPositionAndPreservesIdentity()
    {
        var plant = CreatePlant();
        var originalId = plant.Id;
        var originalFarmId = plant.FarmId;
        var originalZoneId = plant.ZoneId;
        var originalCode = plant.PlantCode;
        var originalHealthLevelId = plant.CurrentHealthLevelId;
        var originalCreatedFromMissionId = plant.CreatedFromMissionId;
        var originalCreatedAt = plant.CreatedAt;
        var newMapVersionId = Guid.NewGuid();
        var newLocation = CreatePoint(106.7015, 10.7775);

        plant.ApplyPublishedMapPosition(
            newMapVersionId,
            newLocation,
            rowIndex: 3,
            columnIndex: 4,
            locationAccuracyM: 0.875m,
            positionConfidence: 0.9654m,
            RemappedAt);

        Assert.Equal(originalId, plant.Id);
        Assert.Equal(originalFarmId, plant.FarmId);
        Assert.Equal(originalZoneId, plant.ZoneId);
        Assert.Equal(originalCode, plant.PlantCode);
        Assert.Equal(originalHealthLevelId, plant.CurrentHealthLevelId);
        Assert.Equal(
            originalCreatedFromMissionId,
            plant.CreatedFromMissionId);
        Assert.Equal(originalCreatedAt, plant.CreatedAt);
        Assert.Equal(PlantLifecycleStatus.Active, plant.LifecycleStatus);
        Assert.Null(plant.LastInspectedAt);
        Assert.Null(plant.RetiredAt);

        Assert.Equal(newMapVersionId, plant.CurrentMapVersionId);
        Assert.Equal(3, plant.RowIndex);
        Assert.Equal(4, plant.ColumnIndex);
        Assert.Equal(0.875m, plant.LocationAccuracyM);
        Assert.Equal(0.9654m, plant.PositionConfidence);
        Assert.Equal(PositionSource.MappingAi, plant.PositionSource);
        Assert.Equal(RemappedAt, plant.MappedAt);
        Assert.Equal(RemappedAt, plant.UpdatedAt);

        var storedLocation = Assert.IsType<Point>(plant.Location);
        Assert.NotSame(newLocation, storedLocation);
        Assert.Equal(newLocation.X, storedLocation.X);
        Assert.Equal(newLocation.Y, storedLocation.Y);
        Assert.Equal(newLocation.SRID, storedLocation.SRID);
    }

    [Fact]
    public void ApplyPublishedMapPositionCanMapManualPlantWithoutGrid()
    {
        var plant = Plant.RegisterManually(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "MANUAL-001",
            CreatePoint(),
            mapVersionId: null,
            rowIndex: null,
            columnIndex: null,
            locationAccuracyM: null,
            Guid.NewGuid(),
            CreatedAt);

        plant.ApplyPublishedMapPosition(
            Guid.NewGuid(),
            CreatePoint(106.702, 10.778),
            rowIndex: 1,
            columnIndex: 1,
            locationAccuracyM: 1m,
            positionConfidence: 0.9m,
            RemappedAt);

        Assert.NotNull(plant.CurrentMapVersionId);
        Assert.Equal(1, plant.RowIndex);
        Assert.Equal(1, plant.ColumnIndex);
        Assert.Equal(PositionSource.MappingAi, plant.PositionSource);
        Assert.Equal(RemappedAt, plant.MappedAt);
        Assert.Null(plant.CreatedFromMissionId);
    }

    [Fact]
    public void ApplyPublishedMapPositionRejectsEmptyMapVersionIdentifier()
    {
        var plant = CreatePlant();

        var exception = Assert.Throws<ArgumentException>(() =>
            ApplyPosition(plant, mapVersionId: Guid.Empty));

        Assert.Equal("mapVersionId", exception.ParamName);
    }

    [Fact]
    public void ApplyPublishedMapPositionRejectsNullLocation()
    {
        var plant = CreatePlant();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            ApplyPosition(plant, location: null, useNullLocation: true));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void ApplyPublishedMapPositionRejectsEmptyLocation()
    {
        var plant = CreatePlant();
        var geometryFactory = new GeometryFactory(
            new PrecisionModel(),
            4326);
        var location = geometryFactory.CreatePoint((Coordinate)null!);

        var exception = Assert.Throws<ArgumentException>(() =>
            ApplyPosition(plant, location: location));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void ApplyPublishedMapPositionRejectsUnexpectedSrid()
    {
        var plant = CreatePlant();
        var location = CreatePoint();
        location.SRID = 3857;

        var exception = Assert.Throws<ArgumentException>(() =>
            ApplyPosition(plant, location: location));

        Assert.Equal("location", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN, 10.0)]
    [InlineData(double.PositiveInfinity, 10.0)]
    [InlineData(double.NegativeInfinity, 10.0)]
    [InlineData(106.0, double.NaN)]
    [InlineData(106.0, double.PositiveInfinity)]
    [InlineData(106.0, double.NegativeInfinity)]
    public void ApplyPublishedMapPositionRejectsNonFiniteLocation(
        double longitude,
        double latitude)
    {
        var plant = CreatePlant();

        var exception = Assert.Throws<ArgumentException>(() =>
            ApplyPosition(
                plant,
                location: CreatePoint(longitude, latitude)));

        Assert.Equal("location", exception.ParamName);
    }

    [Theory]
    [InlineData(-180.0001, 0.0)]
    [InlineData(180.0001, 0.0)]
    [InlineData(0.0, -90.0001)]
    [InlineData(0.0, 90.0001)]
    public void ApplyPublishedMapPositionRejectsLocationOutsideBounds(
        double longitude,
        double latitude)
    {
        var plant = CreatePlant();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ApplyPosition(
                plant,
                location: CreatePoint(longitude, latitude)));

        Assert.Equal("location", exception.ParamName);
    }

    [Theory]
    [InlineData(-180.0, -90.0)]
    [InlineData(180.0, 90.0)]
    public void ApplyPublishedMapPositionAcceptsLocationAtBounds(
        double longitude,
        double latitude)
    {
        var plant = CreatePlant();

        ApplyPosition(
            plant,
            location: CreatePoint(longitude, latitude));

        var storedLocation = Assert.IsType<Point>(plant.Location);
        Assert.Equal(longitude, storedLocation.X);
        Assert.Equal(latitude, storedLocation.Y);
    }

    [Theory]
    [InlineData(0, 1, "rowIndex")]
    [InlineData(-1, 1, "rowIndex")]
    [InlineData(1, 0, "columnIndex")]
    [InlineData(1, -1, "columnIndex")]
    public void ApplyPublishedMapPositionRejectsNonPositiveGridIndex(
        int rowIndex,
        int columnIndex,
        string expectedParameterName)
    {
        var plant = CreatePlant();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ApplyPosition(
                plant,
                rowIndex: rowIndex,
                columnIndex: columnIndex));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void ApplyPublishedMapPositionAcceptsNullAndBoundaryAccuracy()
    {
        var plantWithoutAccuracy = CreatePlant();
        var plantAtMaximumAccuracy = CreatePlant();

        ApplyPosition(plantWithoutAccuracy, locationAccuracyM: null);
        ApplyPosition(
            plantAtMaximumAccuracy,
            locationAccuracyM: 99_999.999m);

        Assert.Null(plantWithoutAccuracy.LocationAccuracyM);
        Assert.Equal(
            99_999.999m,
            plantAtMaximumAccuracy.LocationAccuracyM);
    }

    [Theory]
    [InlineData("-0.001")]
    [InlineData("100000.000")]
    public void ApplyPublishedMapPositionRejectsAccuracyOutsideRange(
        string accuracyText)
    {
        var plant = CreatePlant();
        var accuracy = decimal.Parse(
            accuracyText,
            System.Globalization.CultureInfo.InvariantCulture);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ApplyPosition(plant, locationAccuracyM: accuracy));

        Assert.Equal("locationAccuracyM", exception.ParamName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.5")]
    [InlineData("1")]
    public void ApplyPublishedMapPositionAcceptsConfidenceWithinRange(
        string confidenceText)
    {
        var plant = CreatePlant();
        var confidence = decimal.Parse(
            confidenceText,
            System.Globalization.CultureInfo.InvariantCulture);

        ApplyPosition(plant, positionConfidence: confidence);

        Assert.Equal(confidence, plant.PositionConfidence);
    }

    [Theory]
    [InlineData("-0.0001")]
    [InlineData("1.0001")]
    public void ApplyPublishedMapPositionRejectsConfidenceOutsideRange(
        string confidenceText)
    {
        var plant = CreatePlant();
        var confidence = decimal.Parse(
            confidenceText,
            System.Globalization.CultureInfo.InvariantCulture);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ApplyPosition(plant, positionConfidence: confidence));

        Assert.Equal("positionConfidence", exception.ParamName);
    }

    [Fact]
    public void ApplyPublishedMapPositionRejectsDefaultTimestamp()
    {
        var plant = CreatePlant();

        var exception = Assert.Throws<ArgumentException>(() =>
            ApplyPosition(plant, mappedAt: default(DateTimeOffset)));

        Assert.Equal("mappedAt", exception.ParamName);
    }

    [Fact]
    public void ApplyPublishedMapPositionRejectsNonUtcTimestamp()
    {
        var plant = CreatePlant();
        var nonUtcTimestamp =
            new DateTimeOffset(2026, 9, 18, 17, 0, 0, TimeSpan.FromHours(7));

        var exception = Assert.Throws<ArgumentException>(() =>
            ApplyPosition(plant, mappedAt: nonUtcTimestamp));

        Assert.Equal("mappedAt", exception.ParamName);
    }

    [Theory]
    [InlineData(PlantLifecycleStatus.Missing)]
    [InlineData(PlantLifecycleStatus.Removed)]
    [InlineData(PlantLifecycleStatus.Dead)]
    [InlineData(PlantLifecycleStatus.Inactive)]
    public void ApplyPublishedMapPositionRejectsNonActivePlant(
        PlantLifecycleStatus lifecycleStatus)
    {
        var plant = CreatePlant();
        SetLifecycleStatus(plant, lifecycleStatus);
        var originalMapVersionId = plant.CurrentMapVersionId;
        var originalLocation = Assert.IsType<Point>(plant.Location);
        var originalRowIndex = plant.RowIndex;
        var originalColumnIndex = plant.ColumnIndex;
        var originalUpdatedAt = plant.UpdatedAt;

        Assert.Throws<InvalidOperationException>(() =>
            ApplyPosition(plant));

        Assert.Equal(originalMapVersionId, plant.CurrentMapVersionId);
        Assert.Same(originalLocation, plant.Location);
        Assert.Equal(originalRowIndex, plant.RowIndex);
        Assert.Equal(originalColumnIndex, plant.ColumnIndex);
        Assert.Equal(originalUpdatedAt, plant.UpdatedAt);
        Assert.Equal(lifecycleStatus, plant.LifecycleStatus);
    }

    [Fact]
    public void ApplyPublishedMapPositionDoesNotMutateStateWhenValidationFails()
    {
        var plant = CreatePlant();
        var originalMapVersionId = plant.CurrentMapVersionId;
        var originalLocation = Assert.IsType<Point>(plant.Location);
        var originalRowIndex = plant.RowIndex;
        var originalColumnIndex = plant.ColumnIndex;
        var originalAccuracy = plant.LocationAccuracyM;
        var originalConfidence = plant.PositionConfidence;
        var originalMappedAt = plant.MappedAt;
        var originalUpdatedAt = plant.UpdatedAt;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ApplyPosition(plant, rowIndex: 0));

        Assert.Equal(originalMapVersionId, plant.CurrentMapVersionId);
        Assert.Same(originalLocation, plant.Location);
        Assert.Equal(originalRowIndex, plant.RowIndex);
        Assert.Equal(originalColumnIndex, plant.ColumnIndex);
        Assert.Equal(originalAccuracy, plant.LocationAccuracyM);
        Assert.Equal(originalConfidence, plant.PositionConfidence);
        Assert.Equal(originalMappedAt, plant.MappedAt);
        Assert.Equal(originalUpdatedAt, plant.UpdatedAt);
    }

    private static Plant CreatePlant() =>
        Plant.CreateFromMapping(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "MAP-001",
            CreatePoint(),
            Guid.NewGuid(),
            rowIndex: 1,
            columnIndex: 1,
            locationAccuracyM: 1.5m,
            positionConfidence: 0.9m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAt);

    private static void ApplyPosition(
        Plant plant,
        Guid? mapVersionId = null,
        Point? location = null,
        bool useNullLocation = false,
        int rowIndex = 2,
        int columnIndex = 2,
        decimal? locationAccuracyM = 1m,
        decimal positionConfidence = 0.95m,
        DateTimeOffset? mappedAt = null) =>
        plant.ApplyPublishedMapPosition(
            mapVersionId ?? Guid.NewGuid(),
            useNullLocation ? null! : location ?? CreatePoint(106.701, 10.777),
            rowIndex,
            columnIndex,
            locationAccuracyM,
            positionConfidence,
            mappedAt ?? RemappedAt);

    private static Point CreatePoint(
        double longitude = 106.700981,
        double latitude = 10.776889) =>
        new(longitude, latitude)
        {
            SRID = 4326
        };

    private static void SetLifecycleStatus(
        Plant plant,
        PlantLifecycleStatus lifecycleStatus)
    {
        var property = typeof(Plant).GetProperty(
            nameof(Plant.LifecycleStatus));
        Assert.NotNull(property);
        property.SetValue(plant, lifecycleStatus);
    }
}
