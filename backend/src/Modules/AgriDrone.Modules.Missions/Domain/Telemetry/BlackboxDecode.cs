using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Telemetry;

public sealed class BlackboxDecode : Entity
{
    private BlackboxDecode() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid MissionId { get; private set; }
    public string SourceFileName { get; private set; } = null!;
    public string SourceChecksum { get; private set; } = null!;
    public JsonDocument PointsJson { get; private set; } = null!;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static BlackboxDecode Create(
        Guid tenantId, Guid farmId, Guid missionId,
        string sourceFileName, string sourceChecksum,
        JsonDocument pointsJson, Guid createdBy,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(missionId);
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(createdAt);
        ArgumentNullException.ThrowIfNull(pointsJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceChecksum);

        if (pointsJson.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Decoded telemetry must be a JSON object.", nameof(pointsJson));

        var name = sourceFileName.Trim();
        var checksum = sourceChecksum.Trim().ToLowerInvariant();
        if (name.Length > 255 || name is "." or ".." ||
            name.Contains('/') || name.Contains('\\') ||
            name.Any(char.IsControl))
            throw new ArgumentException("Invalid Blackbox file name.", nameof(sourceFileName));
        if (checksum.Length != 64 || !checksum.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid SHA-256 checksum.", nameof(sourceChecksum));

        return new BlackboxDecode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            MissionId = missionId,
            SourceFileName = name,
            SourceChecksum = checksum,
            PointsJson = JsonDocument.Parse(pointsJson.RootElement.GetRawText()),
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }
}
