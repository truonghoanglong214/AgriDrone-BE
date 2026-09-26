using System.Text.Json;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Characterization;

/// <summary>
/// Locks observable legacy behavior before the Be-Plan migration changes its
/// ownership and operational gates. These assertions describe the baseline;
/// they are not endorsements of the target model.
/// </summary>
public sealed class LegacyOperationalFlowCharacterizationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 22, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "BePlanPhase0Characterization")]
    public void FarmCreationCurrentlyAcceptsDirectTenantOwnedCreation()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var farm = Farm.Create(
            tenantId,
            "FARM-LEGACY",
            "Legacy Farm",
            "Bình Thuận",
            boundary: null,
            centerPoint: null,
            areaHectares: 2.5m,
            GeneralStatus.Active,
            actorId,
            Now);

        Assert.Equal(tenantId, farm.TenantId);
        Assert.Equal("FARM-LEGACY", farm.Code);
        Assert.Equal(actorId, farm.CreatedBy);
        Assert.Equal(GeneralStatus.Active, farm.Status);
        Assert.Equal(1, farm.Version);
    }

    [Fact]
    [Trait("Category", "BePlanPhase3")]
    public void DroneRegistrationUsesSystemOwnership()
    {
        var drone = Drone.Create(
            " drone-01 ",
            " Survey Drone ",
            "Mavic",
            "DJI",
            specifications: null,
            " serial-01 ",
            " vn-001 ",
            registrationDate: null,
            registrationExpiryDate: null,
            weightKg: 1.2m,
            notes: null,
            Now);

        Assert.Null(typeof(Drone).GetProperty("TenantId"));
        Assert.Equal("DRONE-01", drone.Code);
        Assert.Equal("SERIAL-01", drone.SerialNumber);
        Assert.Equal("VN-001", drone.RegistrationNumber);
        Assert.Equal(DroneStatus.Available, drone.Status);

        drone.Deactivate(Now.AddMinutes(1));
        Assert.Equal(DroneStatus.Inactive, drone.Status);

        drone.Reactivate(Now.AddMinutes(2));
        Assert.Equal(DroneStatus.Available, drone.Status);
    }

    [Fact]
    [Trait("Category", "BePlanPhase0Characterization")]
    public void MissionCurrentlyStartsWithoutSurveyOrderOrPaymentGate()
    {
        using var flightParameters = JsonDocument.Parse("{}");
        var actorId = Guid.NewGuid();
        var mission = DroneMission.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            actorId,
            " mission-legacy ",
            MissionType.Mapping,
            sourceMapVersionId: null,
            flightParameters,
            notes: null,
            actorId,
            Now);

        mission.Schedule(
            Now.AddHours(1),
            Now.AddHours(2),
            Now.AddMinutes(1));
        mission.StartFlight(actorId, Now.AddHours(1));

        Assert.Null(mission.SurveyOrderId);
        Assert.Null(mission.MissionPurpose);
        Assert.Equal(MissionStatus.InFlight, mission.Status);
        Assert.Equal(actorId, mission.PreflightConfirmedBy);
        Assert.Equal(Now.AddHours(1), mission.StartedAt);
    }
}
