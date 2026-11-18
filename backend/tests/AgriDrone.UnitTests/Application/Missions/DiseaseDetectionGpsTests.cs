using AgriDrone.Modules.Missions.Application.Features.Processing;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class DiseaseDetectionGpsTests
{
    [Theory]
    [InlineData(11_823_080)] // Regression: (0,0) was compared with the Tam Vu Farm.
    [InlineData(100_000)]
    [InlineData(99_999.9995)] // Would overflow after PostgreSQL rounds to scale 3.
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsUnpersistableGpsDistanceBeforeDatabaseWrite(double distance)
    {
        var error = Assert.Throws<DiseaseDetectionLocationException>(() =>
            DetectMissionDiseasesCommandHandler.GetGpsDistanceMeters(distance, "capture.jpg"));

        Assert.Contains("capture.jpg", error.Message, StringComparison.Ordinal);
        Assert.Contains("GPS", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(2.5126, "2.513")]
    [InlineData(99_999.999, "99999.999")]
    public void KeepsValidDistanceWithoutClamping(double distance, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            DetectMissionDiseasesCommandHandler.GetGpsDistanceMeters(distance, "capture.jpg"));
    }

    [Fact]
    public void NoPlantCandidateHasNoGpsDistance()
    {
        Assert.Null(DetectMissionDiseasesCommandHandler.GetGpsDistanceMeters(null, "capture.jpg"));
    }
}
