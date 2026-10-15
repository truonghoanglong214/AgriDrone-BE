using AgriDrone.SharedKernel.Domain;
using System.Text.Json;

namespace AgriDrone.Modules.Missions.Domain.Telemetry;

public sealed class MissionTelemetryImport : Entity
{
    private MissionTelemetryImport()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid MissionId { get; private set; }

    public Guid OperationId { get; private set; }

    public string SourceFileName { get; private set; } = null!;

    public string PayloadChecksum { get; private set; } = null!;

    public int PointCount { get; private set; }

    public JsonDocument? SelectedSegmentJson { get; private set; }

    public DateTimeOffset FirstRecordedAt { get; private set; }

    public DateTimeOffset LastRecordedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset ImportedAt { get; private set; }

    public static MissionTelemetryImport Create(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid operationId,
        string sourceFileName,
        string payloadChecksum,
        int pointCount,
        JsonDocument selectedSegmentJson,
        DateTimeOffset firstRecordedAt,
        DateTimeOffset lastRecordedAt,
        Guid createdBy,
        DateTimeOffset importedAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(missionId);
        DomainGuard.NotEmpty(operationId);
        DomainGuard.NotEmpty(createdBy);

        DomainGuard.Utc(firstRecordedAt);
        DomainGuard.Utc(lastRecordedAt);
        DomainGuard.Utc(importedAt);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceFileName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            payloadChecksum);

        ArgumentNullException.ThrowIfNull(selectedSegmentJson);

        if (selectedSegmentJson.RootElement.ValueKind != JsonValueKind.Object ||
            !selectedSegmentJson.RootElement.TryGetProperty("points", out var points) ||
            points.ValueKind != JsonValueKind.Array ||
            points.GetArrayLength() != pointCount)
        {
            throw new ArgumentException(
                "Selected segment must contain the imported points.",
                nameof(selectedSegmentJson));
        }

        var normalizedFileName = sourceFileName.Trim();
        var normalizedChecksum =
            payloadChecksum.Trim().ToLowerInvariant();

        if (normalizedFileName.Length > 255 ||
            normalizedFileName is "." or ".." ||
            normalizedFileName.Contains('/') ||
            normalizedFileName.Contains('\\') ||
            normalizedFileName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "SourceFileName must be a plain file name " +
                "with at most 255 characters.",
                nameof(sourceFileName));
        }

        if (normalizedChecksum.Length != 64 ||
            !normalizedChecksum.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "PayloadChecksum must be a SHA256 " +
                "hexadecimal value.",
                nameof(payloadChecksum));
        }

        if (pointCount < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pointCount),
                "At least two telemetry points are required.");
        }

        if (lastRecordedAt <= firstRecordedAt)
        {
            throw new ArgumentException(
                "LastRecordedAt must be later than FirstRecordedAt.",
                nameof(lastRecordedAt));
        }

        return new MissionTelemetryImport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            MissionId = missionId,
            OperationId = operationId,
            SourceFileName = normalizedFileName,
            PayloadChecksum = normalizedChecksum,
            PointCount = pointCount,
            SelectedSegmentJson = JsonDocument.Parse(
                selectedSegmentJson.RootElement.GetRawText()),
            FirstRecordedAt = firstRecordedAt,
            LastRecordedAt = lastRecordedAt,
            CreatedBy = createdBy,
            ImportedAt = importedAt
        };
    }

    public void ReplaceWith(MissionTelemetryImport replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (replacement.TenantId != TenantId ||
            replacement.FarmId != FarmId ||
            replacement.MissionId != MissionId)
        {
            throw new ArgumentException(
                "Replacement must belong to the same Mission.",
                nameof(replacement));
        }

        OperationId = replacement.OperationId;
        SourceFileName = replacement.SourceFileName;
        PayloadChecksum = replacement.PayloadChecksum;
        PointCount = replacement.PointCount;
        FirstRecordedAt = replacement.FirstRecordedAt;
        LastRecordedAt = replacement.LastRecordedAt;
        SelectedSegmentJson = replacement.SelectedSegmentJson;
        CreatedBy = replacement.CreatedBy;
        ImportedAt = replacement.ImportedAt;
    }

    public bool MatchesPayload(
        string sourceFileName,
        string payloadChecksum,
        int pointCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceFileName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            payloadChecksum);

        return string.Equals(
                   SourceFileName,
                   sourceFileName.Trim(),
                   StringComparison.Ordinal) &&
               string.Equals(
                   PayloadChecksum,
                   payloadChecksum.Trim(),
                   StringComparison.OrdinalIgnoreCase) &&
               PointCount == pointCount;
    }

    public bool MatchesPayload(
        string sourceFileName,
        string payloadChecksum,
        int pointCount,
        JsonDocument selectedSegmentJson)
    {
        ArgumentNullException.ThrowIfNull(selectedSegmentJson);

        return MatchesPayload(
                   sourceFileName,
                   payloadChecksum,
                   pointCount) &&
               SelectedSegmentJson is not null &&
               JsonElement.DeepEquals(
                   SelectedSegmentJson.RootElement,
                   selectedSegmentJson.RootElement);
    }
}
