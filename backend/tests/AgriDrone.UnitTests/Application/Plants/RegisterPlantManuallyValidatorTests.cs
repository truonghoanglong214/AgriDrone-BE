using AgriDrone.Modules.Plants.Application.Features.RegisterPlantManually;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Plants;

public sealed class RegisterPlantManuallyValidatorTests
{
    private readonly RegisterPlantManuallyCommandValidator _validator = new();

    [Fact]
    public void ValidCommandWithoutGridPassesValidation()
    {
        var result = _validator.Validate(CreateCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidCommandWithGridPassesValidation()
    {
        var result = _validator.Validate(
            CreateCommand(
                mapVersionId: Guid.NewGuid(),
                rowIndex: 1,
                columnIndex: 1));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void IncompleteGridFailsValidation(
        bool includeMapVersion,
        bool includeRowIndex,
        bool includeColumnIndex)
    {
        var result = _validator.Validate(
            CreateCommand(
                mapVersionId: includeMapVersion ? Guid.NewGuid() : null,
                rowIndex: includeRowIndex ? 1 : null,
                columnIndex: includeColumnIndex ? 1 : null));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage.Contains(
                "must either all be provided or all be omitted",
                StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidLocationFailsValidation()
    {
        var location = CreatePoint();
        location.SRID = 3857;

        var result = _validator.Validate(CreateCommand(location: location));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "Location");
    }

    [Fact]
    public void InvalidCodeAccuracyAndReasonFailValidation()
    {
        var command = CreateCommand(
            plantCode: new string('A', 51),
            locationAccuracyM: -1m,
            reason: " ");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "PlantCode");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "LocationAccuracyM");
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == "Reason");
    }

    private static RegisterPlantManuallyCommand CreateCommand(
        string plantCode = "MANUAL-001",
        Point? location = null,
        Guid? mapVersionId = null,
        int? rowIndex = null,
        int? columnIndex = null,
        decimal? locationAccuracyM = 1m,
        string reason = "Field inspection") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            plantCode,
            location ?? CreatePoint(),
            mapVersionId,
            rowIndex,
            columnIndex,
            locationAccuracyM,
            reason);

    private static Point CreatePoint() =>
        new(106.700981, 10.776889)
        {
            SRID = 4326
        };
}
