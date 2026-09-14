using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Media;

public sealed class MediaAsset : Entity
{
    private MediaAsset()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid? FarmId { get; private set; }

    public string Provider { get; private set; } = null!;

    public string StorageKey { get; private set; } = null!;

    public string Url { get; private set; } = null!;

    public MediaType MediaType { get; private set; }

    public string? MimeType { get; private set; }

    public long? FileSizeBytes { get; private set; }

    public int? WidthPx { get; private set; }

    public int? HeightPx { get; private set; }

    public long? DurationMs { get; private set; }

    public string? Checksum { get; private set; }

    public JsonDocument Metadata { get; private set; } = null!;

    public Guid? UploadedBy { get; private set; }

    public DateTimeOffset? RetentionUntil { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public DateTimeOffset? DeletionRequestedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public MediaStorageStatus StorageStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<MissionMedia> MissionMedia { get; private set; } = [];

    public ICollection<MissionPlantObservation> EvidenceObservations { get; private set; } = [];

    public static MediaAsset Create(
    Guid mediaAssetId,
    Guid tenantId,
    Guid farmId,
    string provider,
    string storageKey,
    string storageUri,
    MediaType mediaType,
    string mimeType,
    long fileSizeBytes,
    string checksum,
    Guid uploadedBy,
    DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(mediaAssetId);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(uploadedBy);
        DomainGuard.Utc(createdAt);

        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(checksum);

        var normalizedProvider = provider.Trim();
        var normalizedMimeType = mimeType.Trim().ToLowerInvariant();
        var normalizedChecksum = checksum.Trim().ToLowerInvariant();

        if (normalizedProvider.Length > 30)
        {
            throw new ArgumentException(
                "Provider cannot exceed 30 characters.",
                nameof(provider));
        }

        if (normalizedMimeType.Length > 100)
        {
            throw new ArgumentException(
                "MimeType cannot exceed 100 characters.",
                nameof(mimeType));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            fileSizeBytes);

        if (!Enum.IsDefined(mediaType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mediaType));
        }

        if (normalizedChecksum.Length != 64 ||
            !normalizedChecksum.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "Checksum must be a SHA256 hexadecimal value.",
                nameof(checksum));
        }

        if (!Uri.TryCreate(
                storageUri,
                UriKind.Absolute,
                out _))
        {
            throw new ArgumentException(
                "Storage URI is invalid.",
                nameof(storageUri));
        }

        return new MediaAsset
        {
            Id = mediaAssetId,
            TenantId = tenantId,
            FarmId = farmId,
            Provider = normalizedProvider,
            StorageKey = storageKey.Trim(),
            Url = storageUri.Trim(),
            MediaType = mediaType,
            MimeType = normalizedMimeType,
            FileSizeBytes = fileSizeBytes,
            Checksum = normalizedChecksum,
            Metadata = JsonDocument.Parse("{}"),
            UploadedBy = uploadedBy,
            StorageStatus = MediaStorageStatus.Active,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

}
