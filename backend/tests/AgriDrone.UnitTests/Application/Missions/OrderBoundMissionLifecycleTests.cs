using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class OrderBoundMissionLifecycleTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 28, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OrderBoundMissionCannotStartWithoutCompletedPreflight()
    {
        var mission = CreateScheduledMission();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            mission.StartFlight(Guid.NewGuid(), Now));

        Assert.Contains("pre-flight checklist", exception.Message);
        Assert.Equal(MissionStatus.Scheduled, mission.Status);
    }

    [Fact]
    public void SuitablePreflightAllowsFlightToComplete()
    {
        var mission = CreateScheduledMission();
        using var answers = JsonDocument.Parse("""
            {"battery":true,"propellers":true,"weather":true}
            """);

        mission.CompletePreflight(
            Guid.NewGuid(), "BE2-UC03-v1", answers, true, "Ready",
            Guid.NewGuid(), Now.AddMinutes(-5));
        mission.StartFlight(Guid.NewGuid(), Now);
        mission.CompleteFlight(Now.AddMinutes(30));

        Assert.Equal(MissionStatus.FlightCompleted, mission.Status);
        Assert.True(mission.PreflightSuitableForFlight);
        Assert.Equal("BE2-UC03-v1", mission.PreflightChecklistVersion);
    }

    [Fact]
    public void UnsuitablePreflightBlocksFlightAndSnapshotCannotBeReplaced()
    {
        var mission = CreateScheduledMission();
        using var answers = JsonDocument.Parse("""{"battery":false}""");
        mission.CompletePreflight(
            Guid.NewGuid(), "BE2-UC03-v1", answers, false, "Low battery",
            Guid.NewGuid(), Now.AddMinutes(-5));

        Assert.Throws<InvalidOperationException>(() =>
            mission.StartFlight(Guid.NewGuid(), Now));
        Assert.Throws<InvalidOperationException>(() =>
            mission.CompletePreflight(
                Guid.NewGuid(), "BE2-UC03-v1", answers, true, null,
                Guid.NewGuid(), Now));
    }

    [Fact]
    public void StalePreflightMustBeRecompletedBeforeFlight()
    {
        var mission = CreateScheduledMission();
        using var oldAnswers = JsonDocument.Parse("""{"battery":true}""");
        using var newAnswers = JsonDocument.Parse("""{"battery":true,"weather":true}""");
        mission.CompletePreflight(Guid.NewGuid(), "v1", oldAnswers, true, null,
            Guid.NewGuid(), Now.AddMinutes(-8));

        mission.ReplaceStalePreflight(Now.AddMinutes(-6));
        Assert.Throws<InvalidOperationException>(() => mission.StartFlight(Guid.NewGuid(), Now));

        mission.CompletePreflight(Guid.NewGuid(), "v2", newAnswers, true, null,
            Guid.NewGuid(), Now.AddMinutes(-4));
        mission.StartFlight(Guid.NewGuid(), Now);
        Assert.Equal("v2", mission.PreflightChecklistVersion);
        Assert.Equal(MissionStatus.InFlight, mission.Status);
    }

    [Fact]
    public void SupersededChecklistRetainsItsImmutableAnswers()
    {
        using var items = JsonDocument.Parse("""[{"key":"battery"}]""");
        using var answers = JsonDocument.Parse("""{"battery":true}""");
        var definition = PreflightChecklistDefinition.CreateActive(
            "DRONE_PRE_FLIGHT", 1, items, Guid.NewGuid(), Now.AddHours(-1));
        var checklist = MissionPreflightChecklist.Complete(
            Guid.NewGuid(), definition, Guid.NewGuid(), answers,
            null, null, Guid.NewGuid(), Now.AddMinutes(-5), Now.AddMinutes(-4));

        checklist.Supersede();

        Assert.Equal(MissionPreflightChecklistStatus.Superseded, checklist.Status);
        Assert.Equal("""{"battery":true}""", checklist.Responses.RootElement.GetRawText());
    }

    [Fact]
    public void ReschedulingInvalidatesEarlierPreflight()
    {
        var mission = CreateScheduledMission();
        using var answers = JsonDocument.Parse("""{"battery":true}""");
        mission.CompletePreflight(Guid.NewGuid(), "v1", answers, true, null,
            Guid.NewGuid(), Now.AddMinutes(-5));

        Assert.True(mission.Reschedule(Now.AddHours(1), Now.AddHours(2), Now));

        Assert.Null(mission.PreflightOperationId);
        Assert.Null(mission.PreflightChecklistAnswers);
        Assert.Throws<InvalidOperationException>(() =>
            mission.StartFlight(Guid.NewGuid(), Now.AddHours(1)));
        Assert.False(mission.Reschedule(Now.AddHours(1), Now.AddHours(2), Now));
    }

    private static DroneMission CreateScheduledMission()
    {
        using var parameters = JsonDocument.Parse("{}");
        var mission = DroneMission.CreateOrderBound(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            [Guid.NewGuid()],
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BE2-UC03-TEST",
            MissionPurpose.PlantHealth,
            Guid.NewGuid(),
            Guid.NewGuid(),
            false,
            parameters,
            Guid.NewGuid(),
            Now.AddHours(-1));
        mission.Schedule(Now.AddMinutes(-10), Now.AddHours(1), Now.AddHours(-1));
        return mission;
    }
}
