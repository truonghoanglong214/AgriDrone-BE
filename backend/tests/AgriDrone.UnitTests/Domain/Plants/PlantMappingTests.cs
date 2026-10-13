using AgriDrone.Modules.Plants.Domain.Plants;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Plants;

public sealed class PlantMappingTests
{
    private static readonly DateTimeOffset MappedAt =
        new(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateFromMappingInitializesMappedPlantAndNormalizesCode()
    {
        var id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var mapVersionId = Guid.NewGuid();
        var healthLevelId = Guid.NewGuid();
        var sourceMissionId = Guid.NewGuid();
        var location = CreatePoint(106.700981, 10.776889);

        var plant = Plant.CreateFromMapping(
            id,
            farmId,
            zoneId,
            " map-001 ",
            location,
            mapVersionId,
            rowIndex: 1,
            columnIndex: 2,
            locationAccuracyM: 1.234m,
            positionConfidence: 0.9876m,
            healthLevelId,
            sourceMissionId,
            MappedAt);

        Assert.Equal(id, plant.Id);
        Assert.Equal(farmId, plant.FarmId);
        Assert.Equal(zoneId, plant.ZoneId);
        Assert.Equal("MAP-001", plant.PlantCode);
        Assert.Equal(mapVersionId, plant.CurrentMapVersionId);
        Assert.Equal(1, plant.RowIndex);
        Assert.Equal(2, plant.ColumnIndex);
        Assert.Equal(1.234m, plant.LocationAccuracyM);
        Assert.Equal(0.9876m, plant.PositionConfidence);
        Assert.Equal(PositionSource.MappingAi, plant.PositionSource);
        Assert.Equal(PlantLifecycleStatus.Active, plant.LifecycleStatus);
        Assert.Equal(healthLevelId, plant.CurrentHealthLevelId);
        Assert.Equal(sourceMissionId, plant.CreatedFromMissionId);
        Assert.Equal(MappedAt, plant.MappedAt);
        Assert.Equal(MappedAt, plant.CreatedAt);
        Assert.Equal(MappedAt, plant.UpdatedAt);
        Assert.Null(plant.LastInspectedAt);
        Assert.Null(plant.RetiredAt);

        var storedLocation = Assert.IsType<Point>(plant.Location);
        Assert.NotSame(location, storedLocation);
        Assert.Equal(location.X, storedLocation.X);
        Assert.Equal(location.Y, storedLocation.Y);
        Assert.Equal(4326, storedLocation.SRID);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateFromMappingRejectsMissingPlantCode(string? plantCode)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(plantCode: plantCode));

        Assert.Equal("plantCode", exception.ParamName);
    }

    [Fact]
    public void CreateFromMappingAcceptsPlantCodeAtMaximumLength()
    {
        var plantCode = new string('a', 50);

        var plant = CreatePlant(plantCode: plantCode);

        Assert.Equal(new string('A', 50), plant.PlantCode);
    }

    [Fact]
    public void CreateFromMappingRejectsPlantCodeOverMaximumLength()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(plantCode: new string('a', 51)));

        Assert.Equal("plantCode", exception.ParamName);
    }

    [Theory]
    [InlineData(RequiredIdentifier.Id, "id")]
    [InlineData(RequiredIdentifier.FarmId, "farmId")]
    [InlineData(RequiredIdentifier.ZoneId, "zoneId")]
    [InlineData(RequiredIdentifier.MapVersionId, "mapVersionId")]
    [InlineData(RequiredIdentifier.HealthLevelId, "healthLevelId")]
    [InlineData(RequiredIdentifier.SourceMissionId, "sourceMissionId")]
    public void CreateFromMappingRejectsEmptyRequiredIdentifier(
        RequiredIdentifier identifier,
        string expectedParameterName)
    {
        var id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var mapVersionId = Guid.NewGuid();
        var healthLevelId = Guid.NewGuid();
        var sourceMissionId = Guid.NewGuid();

        switch (identifier)
        {
            case RequiredIdentifier.Id:
                id = Guid.Empty;
                break;
            case RequiredIdentifier.FarmId:
                farmId = Guid.Empty;
                break;
            case RequiredIdentifier.ZoneId:
                zoneId = Guid.Empty;
                break;
            case RequiredIdentifier.MapVersionId:
                mapVersionId = Guid.Empty;
                break;
            case RequiredIdentifier.HealthLevelId:
                healthLevelId = Guid.Empty;
                break;
            case RequiredIdentifier.SourceMissionId:
                sourceMissionId = Guid.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(identifier));
        }

        var exception = Assert.Throws<ArgumentException>(() =>
            Plant.CreateFromMapping(
                id,
                farmId,
                zoneId,
                "MAP-001",
                CreatePoint(),
                mapVersionId,
                rowIndex: 1,
                columnIndex: 1,
                locationAccuracyM: null,
                positionConfidence: 0.9m,
                healthLevelId,
                sourceMissionId,
                MappedAt));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void CreateFromMappingRejectsNullLocation()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreatePlant(location: null, useNullLocation: true));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void CreateFromMappingRejectsEmptyLocation()
    {
        var geometryFactory = new GeometryFactory(
            new PrecisionModel(),
            4326);
        var location = geometryFactory.CreatePoint((Coordinate)null!);

        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(location: location));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void CreateFromMappingRejectsLocationWithUnexpectedSrid()
    {
        var location = CreatePoint();
        location.SRID = 3857;

        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(location: location));

        Assert.Equal("location", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN, 10.0)]
    [InlineData(double.PositiveInfinity, 10.0)]
    [InlineData(double.NegativeInfinity, 10.0)]
    [InlineData(106.0, double.NaN)]
    [InlineData(106.0, double.PositiveInfinity)]
    [InlineData(106.0, double.NegativeInfinity)]
    public void CreateFromMappingRejectsNonFiniteLocation(
        double longitude,
        double latitude)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(location: CreatePoint(longitude, latitude)));

        Assert.Equal("location", exception.ParamName);
    }

    [Theory]
    [InlineData(-180.0001, 0.0)]
    [InlineData(180.0001, 0.0)]
    [InlineData(0.0, -90.0001)]
    [InlineData(0.0, 90.0001)]
    public void CreateFromMappingRejectsLocationOutsideCoordinateBounds(
        double longitude,
        double latitude)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatePlant(location: CreatePoint(longitude, latitude)));

        Assert.Equal("location", exception.ParamName);
    }

    [Theory]
    [InlineData(-180.0, -90.0)]
    [InlineData(180.0, 90.0)]
    public void CreateFromMappingAcceptsLocationAtCoordinateBounds(
        double longitude,
        double latitude)
    {
        var plant = CreatePlant(location: CreatePoint(longitude, latitude));

        var storedLocation = Assert.IsType<Point>(plant.Location);
        Assert.Equal(longitude, storedLocation.X);
        Assert.Equal(latitude, storedLocation.Y);
    }

    [Theory]
    [InlineData(0, 1, "rowIndex")]
    [InlineData(-1, 1, "rowIndex")]
    [InlineData(1, 0, "columnIndex")]
    [InlineData(1, -1, "columnIndex")]
    public void CreateFromMappingRejectsNonPositiveGridIndex(
        int rowIndex,
        int columnIndex,
        string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatePlant(
                rowIndex: rowIndex,
                columnIndex: columnIndex));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void CreateFromMappingAcceptsNullAndBoundaryLocationAccuracy()
    {
        var plantWithoutAccuracy = CreatePlant(locationAccuracyM: null);
        var plantAtMaximumAccuracy =
            CreatePlant(locationAccuracyM: 99_999.999m);

        Assert.Null(plantWithoutAccuracy.LocationAccuracyM);
        Assert.Equal(
            99_999.999m,
            plantAtMaximumAccuracy.LocationAccuracyM);
    }

    [Theory]
    [InlineData("-0.001")]
    [InlineData("100000.000")]
    public void CreateFromMappingRejectsLocationAccuracyOutsideRange(
        string accuracyText)
    {
        var accuracy = decimal.Parse(
            accuracyText,
            System.Globalization.CultureInfo.InvariantCulture);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatePlant(locationAccuracyM: accuracy));

        Assert.Equal("locationAccuracyM", exception.ParamName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.5")]
    [InlineData("1")]
    public void CreateFromMappingAcceptsPositionConfidenceWithinRange(
        string confidenceText)
    {
        var confidence = decimal.Parse(
            confidenceText,
            System.Globalization.CultureInfo.InvariantCulture);

        var plant = CreatePlant(positionConfidence: confidence);

        Assert.Equal(confidence, plant.PositionConfidence);
    }

    [Theory]
    [InlineData("-0.0001")]
    [InlineData("1.0001")]
    public void CreateFromMappingRejectsPositionConfidenceOutsideRange(
        string confidenceText)
    {
        var confidence = decimal.Parse(
            confidenceText,
            System.Globalization.CultureInfo.InvariantCulture);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatePlant(positionConfidence: confidence));

        Assert.Equal("positionConfidence", exception.ParamName);
    }

    [Fact]
    public void CreateFromMappingRejectsDefaultTimestamp()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(mappedAt: default(DateTimeOffset)));

        Assert.Equal("mappedAt", exception.ParamName);
    }

    [Fact]
    public void CreateFromMappingRejectsNonUtcTimestamp()
    {
        var nonUtcTimestamp =
            new DateTimeOffset(2026, 9, 18, 15, 0, 0, TimeSpan.FromHours(7));

        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(mappedAt: nonUtcTimestamp));

        Assert.Equal("mappedAt", exception.ParamName);
    }

    private static Plant CreatePlant(
        string? plantCode = "MAP-001",
        Point? location = null,
        bool useNullLocation = false,
        int rowIndex = 1,
        int columnIndex = 1,
        decimal? locationAccuracyM = 1.5m,
        decimal positionConfidence = 0.9m,
        DateTimeOffset? mappedAt = null) =>
        Plant.CreateFromMapping(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            plantCode!,
            useNullLocation ? null! : location ?? CreatePoint(),
            Guid.NewGuid(),
            rowIndex,
            columnIndex,
            locationAccuracyM,
            positionConfidence,
            Guid.NewGuid(),
            Guid.NewGuid(),
            mappedAt ?? MappedAt);

    private static Point CreatePoint(
        double longitude = 106.700981,
        double latitude = 10.776889) =>
        new(longitude, latitude)
        {
            SRID = 4326
        };

    public enum RequiredIdentifier
    {
        Id,
        FarmId,
        ZoneId,
        MapVersionId,
        HealthLevelId,
        SourceMissionId
    }
}
