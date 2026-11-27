using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Processing;
using AgriDrone.Modules.Missions.Domain.Processing;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class MappingGridEstimatorTests
{
    private const double DegreesPerMeter = 180.0 / (Math.PI * 6_371_000.0);

    [Fact]
    public void EstimateUsesDetectedPointsAndAccountsForSkippedColumnsAndStaggeredRows()
    {
        MissionPlantGpsRow[] rows =
        [
            Tree(1, 1, east: 0, north: 0),
            Tree(1, 2, east: -2.5, north: 20) with { Status = "MISSING" },
            Tree(1, 3, east: -5, north: 0),
            Tree(2, 1, east: -2.5, north: 3),
            Tree(2, 2, east: -5, north: 3),
            Tree(2, 3, east: -7.5, north: 3)
        ];

        var grid = MappingGridEstimator.Estimate(rows);

        Assert.InRange(grid.BearingDeg!.Value, 269.99, 270.01);
        Assert.InRange(grid.PlantSpacingM!.Value, 2.499, 2.501);
        Assert.InRange(grid.RowSpacingM!.Value, 2.999, 3.001);
    }

    [Fact]
    public void EstimateLeavesUnknownMeasurementsUnset()
    {
        MissionPlantGpsRow[] rows =
        [
            Tree(1, 1, east: 0, north: 0),
            Tree(1, 2, east: 2.5, north: 0),
            Tree(1, 3, east: 5, north: 0)
        ];

        var grid = MappingGridEstimator.Estimate(rows);

        Assert.Equal(90d, grid.BearingDeg);
        Assert.Null(grid.RowSpacingM);
        Assert.InRange(grid.PlantSpacingM!.Value, 2.499, 2.501);
    }

    [Fact]
    public void EstimateLeavesGridUnsetWhenThereAreCompetingRowAxes()
    {
        MissionPlantGpsRow[] rows =
        [
            Tree(1, 1, east: 0, north: 0),
            Tree(1, 2, east: 2.5, north: 0),
            Tree(2, 1, east: 0, north: 3),
            Tree(2, 2, east: 0, north: 5.5)
        ];

        var grid = MappingGridEstimator.Estimate(rows);

        Assert.Null(grid.BearingDeg);
        Assert.Null(grid.RowSpacingM);
        Assert.Null(grid.PlantSpacingM);
    }

    [Fact]
    public void ReplayMatchesSubmittedGridEvenWhenJobCarriesEstimatedValues()
    {
        var submission = new MappingProcessingSnapshot(1, 0, 3, 2.5, "grid-v1", "input", "output");
        var completed = submission with
        {
            GridBearingDeg = 270,
            RowSpacingM = 2.9,
            SubmittedGrid = new MappingProcessingSnapshot.GridMeasurements(0, 3, 2.5)
        };

        using var serialized = JsonSerializer.SerializeToDocument(completed);
        var persisted = Assert.IsType<MappingProcessingSnapshot>(serialized.Deserialize<MappingProcessingSnapshot>());

        Assert.True(submission.MatchesSubmission(submission));
        Assert.True(persisted.MatchesSubmission(submission));
        Assert.False(persisted.MatchesSubmission(submission with { GridBearingDeg = 90 }));
    }

    private static MissionPlantGpsRow Tree(int row, int col, double east, double north) => new(
        $"tree_r{row}_c{col}", row, col,
        (decimal)(north * DegreesPerMeter), (decimal)(east * DegreesPerMeter), 1, "DETECTED", "test");
}
