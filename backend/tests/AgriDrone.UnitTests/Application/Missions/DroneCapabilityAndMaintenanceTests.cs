using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class DroneCapabilityAndMaintenanceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DroneOnlySupportsDeclaredMissionPurposes()
    {
        var capabilities = JsonSerializer.Deserialize<JsonElement>(
            """{"capabilities":["baseline_mapping","plant_health"]}""");
        var drone = Drone.Create("CAP-01", "Survey Drone", null, null,
            capabilities, null, null, null, null, null, null, Now);

        Assert.True(drone.SupportsPurpose(MissionPurpose.BaselineMapping));
        Assert.True(drone.SupportsPurpose(MissionPurpose.PlantHealth));
        Assert.False(drone.SupportsPurpose(MissionPurpose.HarvestReadiness));
    }

    [Fact]
    public void LegacyDroneWithoutCapabilitiesIsNotEligibleForPurpose()
    {
        var drone = Drone.Create("CAP-02", "Legacy Drone", null, null,
            null, null, null, null, null, null, null, Now);
        Assert.False(drone.SupportsPurpose(MissionPurpose.PlantHealth));
    }

    [Fact]
    public void InvalidCapabilitiesAreRejectedAtRegistration()
    {
        var invalid = JsonSerializer.Deserialize<JsonElement>(
            """{"capabilities":["plant_health","unknown"]}""");
        Assert.Throws<ArgumentException>(() => Drone.Create(
            "CAP-03", "Invalid Drone", null, null, invalid,
            null, null, null, null, null, null, Now));
    }

    [Fact]
    public void MaintenanceRecordKeepsActorReasonAndClosure()
    {
        var droneId = Guid.NewGuid();
        var starter = Guid.NewGuid();
        var closer = Guid.NewGuid();
        var record = DroneMaintenanceRecord.Start(droneId, Now, starter, "Inspection");
        var next = Now.AddMonths(1);
        record.Close(Now.AddHours(2), closer, DroneStatus.Available, next);

        Assert.Equal(starter, record.StartedBy);
        Assert.Equal("Inspection", record.Reason);
        Assert.Equal(closer, record.ClosedBy);
        Assert.Equal(DroneStatus.Available, record.ClosingStatus);
        Assert.Equal(next, record.NextMaintenanceAt);
        Assert.Throws<InvalidOperationException>(() =>
            record.Close(Now.AddHours(3), closer, DroneStatus.Available, next));
    }
}
