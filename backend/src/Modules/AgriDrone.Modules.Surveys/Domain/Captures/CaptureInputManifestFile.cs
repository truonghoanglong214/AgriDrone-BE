using System.Text.RegularExpressions;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class CaptureInputManifestFile : Entity
{
    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private CaptureInputManifestFile() { }

    public Guid ManifestId { get; private set; }
    public Guid MediaAssetId { get; private set; }
    public string Sha256 { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string MediaRole { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public static CaptureInputManifestFile Create(
        Guid manifestId,
        Guid mediaAssetId,
        string sha256,
        long sizeBytes,
        string mediaRole,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(manifestId, nameof(manifestId));
        DomainGuard.NotEmpty(mediaAssetId, nameof(mediaAssetId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaRole);
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var normalizedSha = sha256.Trim().ToLowerInvariant();
        if (!Hex64Regex.IsMatch(normalizedSha))
        {
            throw new ArgumentException("SHA-256 must be a 64-character lowercase hex string.", nameof(sha256));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "File size must be positive.");
        }

        return new CaptureInputManifestFile
        {
            ManifestId = manifestId,
            MediaAssetId = mediaAssetId,
            Sha256 = normalizedSha,
            SizeBytes = sizeBytes,
            MediaRole = mediaRole.Trim(),
            CreatedAt = createdAt
        };
    }
}
