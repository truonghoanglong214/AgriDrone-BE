using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Missions;

public sealed class CaptureAttemptMissionBindingTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OrderBoundMissionCanBindOneCaptureAttemptIdempotently()
    {
        using var flightParameters = JsonDocument.Parse("{}");
        var mission = DroneMission.CreateOrderBound(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            [],
            farmBoundaryVersionId: null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "CAPTURE-01",
            MissionPurpose.BaselineMapping,
            Guid.NewGuid(),
            sourceMapVersionId: null,
            requiresBaselineCompletion: false,
            flightParameters,
            Guid.NewGuid(),
            Now);
        var attemptId = Guid.NewGuid();

        mission.BindCaptureAttempt(attemptId);
        mission.BindCaptureAttempt(attemptId);

        Assert.Equal(attemptId, mission.CaptureAttemptId);
        Assert.Throws<InvalidOperationException>(() =>
            mission.BindCaptureAttempt(Guid.NewGuid()));
    }

    [Fact]
    public void LegacyMissionCannotBindCaptureAttempt()
    {
        using var flightParameters = JsonDocument.Parse("{}");
        var actorId = Guid.NewGuid();
        var mission = DroneMission.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            actorId,
            "LEGACY-01",
            MissionType.Mapping,
            sourceMapVersionId: null,
            flightParameters,
            notes: null,
            actorId,
            Now);

        Assert.Throws<InvalidOperationException>(() =>
            mission.BindCaptureAttempt(Guid.NewGuid()));
        Assert.Null(mission.CaptureAttemptId);
    }
}
