using AgriDrone.Modules.Farms.Application.Features.ClassifyAndRecordPoint;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class ClassifyAndRecordPointRequestValidatorTests
{
    private readonly ClassifyAndRecordPointRequestValidator _validator = new();
    private readonly GeometryFactory _geometryFactory = new(new PrecisionModel(), 4326);

    [Fact]
    public void ValidateFailsWhenCommandIsNull()
    {
        var request = new ClassifyAndRecordPointRequest(null!);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command");
    }

    [Fact]
    public void ValidateFailsWhenFarmIdIsEmpty()
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(106.0, 10.0));
        var command = new ClassifyAndRecordPointCommand(
            Guid.Empty,
            point,
            BoundaryExceptionSource.Manual,
            "ref-001");
        var request = new ClassifyAndRecordPointRequest(command);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.FarmId");
    }

    [Fact]
    public void ValidateFailsWhenPointIsNull()
    {
        var command = new ClassifyAndRecordPointCommand(
            Guid.NewGuid(),
            null!,
            BoundaryExceptionSource.Manual,
            "ref-001");
        var request = new ClassifyAndRecordPointRequest(command);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.Point");
    }

    [Theory]
    [InlineData(double.NaN, 10.0)]
    [InlineData(106.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 10.0)]
    [InlineData(106.0, double.NegativeInfinity)]
    public async Task ValidateFailsWhenPointCoordinatesAreNotFinite(double lon, double lat)
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(lon, lat));
        var command = new ClassifyAndRecordPointCommand(
            Guid.NewGuid(),
            point,
            BoundaryExceptionSource.Manual,
            "ref-001");
        var request = new ClassifyAndRecordPointRequest(command);

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.Point");
    }

    [Theory]
    [InlineData(-180.1, 10.0)]
    [InlineData(180.1, 10.0)]
    [InlineData(106.0, -90.1)]
    [InlineData(106.0, 90.1)]
    public void ValidateFailsWhenPointCoordinatesAreOutOfRange(double lon, double lat)
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(lon, lat));
        var command = new ClassifyAndRecordPointCommand(
            Guid.NewGuid(),
            point,
            BoundaryExceptionSource.Manual,
            "ref-001");
        var request = new ClassifyAndRecordPointRequest(command);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.Point");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ValidateFailsWhenSourceReferenceIdIsEmpty(string referenceId)
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(106.0, 10.0));
        var command = new ClassifyAndRecordPointCommand(
            Guid.NewGuid(),
            point,
            BoundaryExceptionSource.Manual,
            referenceId);
        var request = new ClassifyAndRecordPointRequest(command);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.SourceReferenceId");
    }

    [Fact]
    public void ValidateFailsWhenSourceReferenceIdExceedsMaxLength()
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(106.0, 10.0));
        var command = new ClassifyAndRecordPointCommand(
            Guid.NewGuid(),
            point,
            BoundaryExceptionSource.Manual,
            new string('A', 201));
        var request = new ClassifyAndRecordPointRequest(command);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.SourceReferenceId");
    }

    [Fact]
    public void ValidateFailsWhenNearBoundaryThresholdIsNegative()
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(106.0, 10.0));
        var command = new ClassifyAndRecordPointCommand(
            Guid.NewGuid(),
            point,
            BoundaryExceptionSource.Manual,
            "ref-001",
            NearBoundaryThresholdMeters: -1.0);
        var request = new ClassifyAndRecordPointRequest(command);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Command.NearBoundaryThresholdMeters");
    }

    [Fact]
    public void ValidateSucceedsWhenValid()
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(106.05, 10.05));
        var command = new ClassifyAndRecordPointCommand(
            Guid.NewGuid(),
            point,
            BoundaryExceptionSource.BaselineCandidate,
            "ref-001",
            SurveyOrderId: Guid.NewGuid(),
            MissionId: Guid.NewGuid(),
            NearBoundaryThresholdMeters: 5.0);
        var request = new ClassifyAndRecordPointRequest(command);

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }
}
