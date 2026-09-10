using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Media;

public sealed class MediaUploadSession : Entity
{
    private MediaUploadSession()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid MissionId { get; private set; }

    public Guid OperationId { get; private set; }

    public Guid MediaAssetId { get; private set; }

    public Guid CreatedBy { get; private set; }

    public string FileName { get; private set; } = null!;

    public string MimeType { get; private set; } = null!;

    public MediaType MediaType { get; private set; }

    public long FileSizeBytes { get; private set; }

    public string ChecksumAlgorithm { get; private set; } = null!;

    public string ExpectedChecksum { get; private set; } = null!;

    public string StorageUri { get; private set; } = null!;

    public MediaUploadSessionStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public uint Version { get; private set; }

    public static MediaUploadSession Create(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid operationId,
        Guid mediaAssetId,
        Guid createdBy,
        string fileName,
        string mimeType,
        MediaType mediaType,
        long fileSizeBytes,
        string expectedChecksum,
        string storageUri,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(missionId);
        DomainGuard.NotEmpty(operationId);
        DomainGuard.NotEmpty(mediaAssetId);
        DomainGuard.NotEmpty(createdBy);

        DomainGuard.Utc(createdAt);
        DomainGuard.Utc(expiresAt);

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedChecksum);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageUri);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            fileSizeBytes);

        var normalizedFileName = fileName.Trim();
        var normalizedMimeType = mimeType.Trim().ToLowerInvariant();
        var normalizedChecksum = expectedChecksum.Trim().ToLowerInvariant();

        if (normalizedFileName.Length > 255 ||
            normalizedFileName is "." or ".." ||
            normalizedFileName.Contains('/') ||
            normalizedFileName.Contains('\\') ||
            normalizedFileName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "FileName must be a plain file name with at most 255 characters.",
                nameof(fileName));
        }

        if (normalizedMimeType.Length > 100 ||
            normalizedMimeType.Any(char.IsControl))
        {
            throw new ArgumentException(
                "MimeType is invalid.",
                nameof(mimeType));
        }

        if (!Enum.IsDefined(mediaType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mediaType));
        }

        if (normalizedChecksum.Length != 64 ||
            !normalizedChecksum.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "ExpectedChecksum must be a SHA256 hexadecimal string.",
                nameof(expectedChecksum));
        }

        if (!Uri.TryCreate(
                storageUri,
                UriKind.Absolute,
                out var parsedStorageUri) ||
            !string.IsNullOrEmpty(parsedStorageUri.Query) ||
            !string.IsNullOrEmpty(parsedStorageUri.Fragment) ||
            !string.IsNullOrEmpty(parsedStorageUri.UserInfo))
        {
            throw new ArgumentException(
                "StorageUri must be a stable absolute storage reference.",
                nameof(storageUri));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAt),
                "The session must expire after its creation time.");
        }

        return new MediaUploadSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            MissionId = missionId,
            OperationId = operationId,
            MediaAssetId = mediaAssetId,
            CreatedBy = createdBy,
            FileName = normalizedFileName,
            MimeType = normalizedMimeType,
            MediaType = mediaType,
            FileSizeBytes = fileSizeBytes,
            ChecksumAlgorithm = "SHA256",
            ExpectedChecksum = normalizedChecksum,
            StorageUri = storageUri,
            Status = MediaUploadSessionStatus.Pending,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    public bool MatchesRequest(
        string fileName,
        string mimeType,
        MediaType mediaType,
        long fileSizeBytes,
        string expectedChecksum)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedChecksum);

        return string.Equals(
                   FileName,
                   fileName.Trim(),
                   StringComparison.Ordinal) &&
               string.Equals(
                   MimeType,
                   mimeType.Trim(),
                   StringComparison.OrdinalIgnoreCase) &&
               MediaType == mediaType &&
               FileSizeBytes == fileSizeBytes &&
               string.Equals(
                   ExpectedChecksum,
                   expectedChecksum.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    public void RefreshPendingUpload(
    string storageUri,
    DateTimeOffset expiresAt,
    DateTimeOffset changedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageUri);

        DomainGuard.Utc(expiresAt);
        DomainGuard.Utc(changedAt);

        if (Status != MediaUploadSessionStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Upload session in status '{Status}' cannot be refreshed.");
        }

        if (!string.Equals(
                StorageUri,
                storageUri,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The refreshed upload URL points to a different storage object.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(
            changedAt,
            CreatedAt);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            expiresAt,
            changedAt);

        ExpiresAt = expiresAt;
        UpdatedAt = changedAt;
    }

    public void BeginVerification(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);

        if (Status != MediaUploadSessionStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Upload session in status '{Status}' cannot be verified.");
        }

        if (IsExpiredAt(changedAt))
        {
            throw new InvalidOperationException(
                "Expired upload session cannot be verified.");
        }

        Status = MediaUploadSessionStatus.Verifying;
        UpdatedAt = changedAt;
    }

    public void CompleteVerification(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);

        if (Status != MediaUploadSessionStatus.Verifying)
        {
            throw new InvalidOperationException(
                $"Upload session in status '{Status}' cannot be completed.");
        }

        Status = MediaUploadSessionStatus.Completed;
        UpdatedAt = changedAt;
    }

    public void FailVerification(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);

        if (Status is not MediaUploadSessionStatus.Pending and
            not MediaUploadSessionStatus.Verifying)
        {
            throw new InvalidOperationException(
                $"Upload session in status '{Status}' cannot be failed.");
        }

        Status = MediaUploadSessionStatus.Failed;
        UpdatedAt = changedAt;
    }

    public void MarkExpired(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);

        if (Status is not MediaUploadSessionStatus.Pending and
            not MediaUploadSessionStatus.Verifying)
        {
            throw new InvalidOperationException(
                $"Upload session in status '{Status}' cannot expire.");
        }

        if (!IsExpiredAt(changedAt))
        {
            throw new InvalidOperationException(
                "Upload session has not expired.");
        }

        Status = MediaUploadSessionStatus.Expired;
        UpdatedAt = changedAt;
    }
    public bool IsExpiredAt(DateTimeOffset now)
    {
        DomainGuard.Utc(now);

        return now >= ExpiresAt;
    }
}