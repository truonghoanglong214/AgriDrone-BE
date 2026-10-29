using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Missions;

public sealed class MissionTelemetryImportTests
{
    [Fact]
    public void MatchesPayloadDetectsChangedSegmentWithSameOperationMetadata()
    {
        var recordedAt = new DateTimeOffset(
            2026, 10, 13, 3, 12, 44, TimeSpan.Zero);
        var checksum = new string('a', 64);
        using var firstJson = JsonDocument.Parse(
            """{"segmentIndex":0,"points":[{"lat":10},{"lat":11}]}""");
        using var sameJson = JsonDocument.Parse(
            """{"points":[{"lat":10},{"lat":11}],"segmentIndex":0}""");
        using var changedJson = JsonDocument.Parse(
            """{"segmentIndex":1,"points":[{"lat":12},{"lat":13}]}""");

        var telemetryImport = MissionTelemetryImport.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "blackbox.txt", checksum, 2, firstJson,
            recordedAt, recordedAt.AddSeconds(1),
            Guid.NewGuid(), recordedAt.AddMinutes(1));

        Assert.True(telemetryImport.MatchesPayload(
            "blackbox.txt", checksum, 2, sameJson));
        Assert.False(telemetryImport.MatchesPayload(
            "blackbox.txt", checksum, 2, changedJson));
    }

    [Fact]
    public void ReplaceWithKeepsImportIdAndUpdatesSelectedSegment()
    {
        var recordedAt = new DateTimeOffset(
            2026, 10, 13, 3, 12, 44, TimeSpan.Zero);
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var firstOperationId = Guid.NewGuid();
        var replacementOperationId = Guid.NewGuid();

        using var firstJson = JsonDocument.Parse(
            """{"points":[{"lat":10},{"lat":11}]}""");
        using var replacementJson = JsonDocument.Parse(
            """{"points":[{"lat":12},{"lat":13}]}""");

        var original = MissionTelemetryImport.Create(
            tenantId, farmId, missionId, firstOperationId,
            "first.txt", new string('a', 64), 2, firstJson,
            recordedAt, recordedAt.AddSeconds(1),
            Guid.NewGuid(), recordedAt.AddMinutes(1));
        var replacement = MissionTelemetryImport.Create(
            tenantId, farmId, missionId, replacementOperationId,
            "second.txt", new string('b', 64), 2, replacementJson,
            recordedAt.AddSeconds(2), recordedAt.AddSeconds(3),
            Guid.NewGuid(), recordedAt.AddMinutes(2));
        var originalId = original.Id;

        original.ReplaceWith(replacement);

        Assert.Equal(originalId, original.Id);
        Assert.Equal(replacementOperationId, original.OperationId);
        Assert.Equal("second.txt", original.SourceFileName);
        Assert.Equal(12, original.SelectedSegmentJson!.RootElement
            .GetProperty("points")[0].GetProperty("lat").GetInt32());
    }

    [Fact]
    public void SelectedSegmentJsonUsesTimestampLatLonAltFormat()
    {
        var recordedAt = new DateTimeOffset(
            2026, 10, 13, 3, 12, 44, TimeSpan.Zero);
        var points = new[]
        {
            new ImportTelemetryPoint(
                0, recordedAt, 106.4939687, 10.4614532,
                3m, null, null, null, null)
        };

        using var segment = ImportTelemetryCommandHandler
            .CreateSelectedSegmentJson(points);
        var point = segment.RootElement
            .GetProperty("points")[0];

        Assert.Equal(JsonValueKind.Null, segment.RootElement
            .GetProperty("segmentIndex").ValueKind);
        Assert.Equal(4, point.EnumerateObject().Count());
        Assert.Equal("2026-10-13 10:12:44", point
            .GetProperty("timestamp").GetString());
        Assert.Equal(10.461453, point.GetProperty("lat").GetDouble());
        Assert.Equal(106.493968, point.GetProperty("lon").GetDouble());
        Assert.Equal(3m, point.GetProperty("alt").GetDecimal());
    }

    [Fact]
    public void CreateKeepsSelectedSegmentAfterSourceDocumentIsDisposed()
    {
        var recordedAt = new DateTimeOffset(
            2026, 10, 13, 3, 12, 44, TimeSpan.Zero);
        MissionTelemetryImport telemetryImport;

        using (var segment = JsonDocument.Parse(
                   """
                   {"segmentIndex":1,"points":[{"sequenceNumber":0},{"sequenceNumber":1}]}
                   """))
        {
            telemetryImport = MissionTelemetryImport.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "blackbox.txt",
                new string('a', 64),
                2,
                segment,
                recordedAt,
                recordedAt.AddSeconds(1),
                Guid.NewGuid(),
                recordedAt.AddMinutes(1));
        }

        Assert.NotNull(telemetryImport.SelectedSegmentJson);
        Assert.Equal(1, telemetryImport.SelectedSegmentJson.RootElement
            .GetProperty("segmentIndex").GetInt32());
        Assert.Equal(2, telemetryImport.SelectedSegmentJson.RootElement
            .GetProperty("points").GetArrayLength());
    }
}
