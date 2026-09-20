using AgriDrone.Modules.Plants.Domain.Plants;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Plants;

public sealed class PlantManualRegistrationTests
{
    private static readonly DateTimeOffset RegisteredAt =
        new(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RegisterManuallyWithoutGridInitializesActiveManualPlant()
    {
        var id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var healthLevelId = Guid.NewGuid();
        var location = CreatePoint();

        var plant = Plant.RegisterManually(
            id,
            farmId,
            zoneId,
            " manual-001 ",
            location,
            mapVersionId: null,
            rowIndex: null,
            columnIndex: null,
            locationAccuracyM: 2.5m,
            healthLevelId,
            RegisteredAt);

        Assert.Equal(id, plant.Id);
        Assert.Equal(farmId, plant.FarmId);
        Assert.Equal(zoneId, plant.ZoneId);
        Assert.Equal("MANUAL-001", plant.PlantCode);
        Assert.Null(plant.CurrentMapVersionId);
        Assert.Null(plant.RowIndex);
        Assert.Null(plant.ColumnIndex);
        Assert.Equal(2.5m, plant.LocationAccuracyM);
        Assert.Null(plant.PositionConfidence);
        Assert.Equal(PositionSource.Manual, plant.PositionSource);
        Assert.Equal(PlantLifecycleStatus.Active, plant.LifecycleStatus);
        Assert.Equal(healthLevelId, plant.CurrentHealthLevelId);
        Assert.Null(plant.MappedAt);
        Assert.Null(plant.CreatedFromMissionId);
        Assert.Equal(RegisteredAt, plant.CreatedAt);
        Assert.Equal(RegisteredAt, plant.UpdatedAt);
        Assert.Null(plant.LastInspectedAt);
        Assert.Null(plant.RetiredAt);

        var storedLocation = Assert.IsType<Point>(plant.Location);
        Assert.NotSame(location, storedLocation);
        Assert.Equal(location.X, storedLocation.X);
        Assert.Equal(location.Y, storedLocation.Y);
        Assert.Equal(4326, storedLocation.SRID);
    }

    [Fact]
    public void RegisterManuallyWithGridStoresCompleteGridPosition()
    {
        var mapVersionId = Guid.NewGuid();

        var plant = CreatePlant(
            mapVersionId: mapVersionId,
            rowIndex: 1,
            columnIndex: 2);

        Assert.Equal(mapVersionId, plant.CurrentMapVersionId);
        Assert.Equal(1, plant.RowIndex);
        Assert.Equal(2, plant.ColumnIndex);
        Assert.Equal(RegisteredAt, plant.MappedAt);
        Assert.Null(plant.PositionConfidence);
        Assert.Null(plant.CreatedFromMissionId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterManuallyRejectsMissingPlantCode(string? plantCode)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(plantCode: plantCode));

        Assert.Equal("plantCode", exception.ParamName);
    }

    [Fact]
    public void RegisterManuallyAcceptsPlantCodeAtMaximumLength()
    {
        var plant = CreatePlant(plantCode: new string('a', 50));

        Assert.Equal(new string('A', 50), plant.PlantCode);
    }

    [Fact]
    public void RegisterManuallyRejectsPlantCodeOverMaximumLength()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(plantCode: new string('a', 51)));

        Assert.Equal("plantCode", exception.ParamName);
    }

    [Theory]
    [InlineData(RequiredIdentifier.Id, "id")]
    [InlineData(RequiredIdentifier.FarmId, "farmId")]
    [InlineData(RequiredIdentifier.ZoneId, "zoneId")]
    [InlineData(RequiredIdentifier.HealthLevelId, "healthLevelId")]
    public void RegisterManuallyRejectsEmptyRequiredIdentifier(
        RequiredIdentifier identifier,
        string expectedParameterName)
    {
        var id = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var healthLevelId = Guid.NewGuid();

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
            case RequiredIdentifier.HealthLevelId:
                healthLevelId = Guid.Empty;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(identifier));
        }

        var exception = Assert.Throws<ArgumentException>(() =>
            Plant.RegisterManually(
                id,
                farmId,
                zoneId,
                "MANUAL-001",
                CreatePoint(),
                mapVersionId: null,
                rowIndex: null,
                columnIndex: null,
                locationAccuracyM: null,
                healthLevelId,
                RegisteredAt));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void RegisterManuallyRejectsIncompleteGridPosition(
        bool includeMapVersion,
        bool includeRowIndex,
        bool includeColumnIndex)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(
                mapVersionId: includeMapVersion ? Guid.NewGuid() : null,
                rowIndex: includeRowIndex ? 1 : null,
                columnIndex: includeColumnIndex ? 1 : null));

        Assert.Equal("mapVersionId", exception.ParamName);
    }

    [Fact]
    public void RegisterManuallyRejectsEmptyMapVersionIdentifier()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(
                mapVersionId: Guid.Empty,
                rowIndex: 1,
                columnIndex: 1));

        Assert.Equal("mapVersionId", exception.ParamName);
    }

    [Theory]
    [InlineData(0, 1, "rowIndex")]
    [InlineData(-1, 1, "rowIndex")]
    [InlineData(1, 0, "columnIndex")]
    [InlineData(1, -1, "columnIndex")]
    public void RegisterManuallyRejectsNonPositiveGridIndex(
        int rowIndex,
        int columnIndex,
        string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatePlant(
                mapVersionId: Guid.NewGuid(),
                rowIndex: rowIndex,
                columnIndex: columnIndex));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public void RegisterManuallyRejectsNullLocation()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            CreatePlant(useNullLocation: true));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void RegisterManuallyRejectsLocationWithUnexpectedSrid()
    {
        var location = CreatePoint();
        location.SRID = 3857;

        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(location: location));

        Assert.Equal("location", exception.ParamName);
    }

    [Fact]
    public void RegisterManuallyAcceptsNullAndBoundaryLocationAccuracy()
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
    public void RegisterManuallyRejectsLocationAccuracyOutsideRange(
        string accuracyText)
    {
        var accuracy = decimal.Parse(
            accuracyText,
            System.Globalization.CultureInfo.InvariantCulture);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreatePlant(locationAccuracyM: accuracy));

        Assert.Equal("locationAccuracyM", exception.ParamName);
    }

    [Fact]
    public void RegisterManuallyRejectsDefaultTimestamp()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(registeredAt: default(DateTimeOffset)));

        Assert.Equal("registeredAt", exception.ParamName);
    }

    [Fact]
    public void RegisterManuallyRejectsNonUtcTimestamp()
    {
        var nonUtcTimestamp =
            new DateTimeOffset(2026, 9, 18, 16, 0, 0, TimeSpan.FromHours(7));

        var exception = Assert.Throws<ArgumentException>(() =>
            CreatePlant(registeredAt: nonUtcTimestamp));

        Assert.Equal("registeredAt", exception.ParamName);
    }

    private static Plant CreatePlant(
        string? plantCode = "MANUAL-001",
        Point? location = null,
        bool useNullLocation = false,
        Guid? mapVersionId = null,
        int? rowIndex = null,
        int? columnIndex = null,
        decimal? locationAccuracyM = 1.5m,
        DateTimeOffset? registeredAt = null) =>
        Plant.RegisterManually(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            plantCode!,
            useNullLocation ? null! : location ?? CreatePoint(),
            mapVersionId,
            rowIndex,
            columnIndex,
            locationAccuracyM,
            Guid.NewGuid(),
            registeredAt ?? RegisteredAt);

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
        HealthLevelId
    }
}
