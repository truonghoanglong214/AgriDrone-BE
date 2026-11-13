using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum CaptureInputManifestStatus
{
    Uploaded,
    Superseded,
    Approved,
    Rejected
}

public sealed class CaptureInputManifest : Entity
{
    private CaptureInputManifest() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid CaptureAttemptId { get; private set; }
    public Guid MissionId { get; private set; }
    public string ManifestFingerprint { get; private set; } = null!;
    public int TotalFileCount { get; private set; }
    public long TotalSizeBytes { get; private set; }
    public string? GpsMetadataSummary { get; private set; }
    public CaptureInputManifestStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EvaluatedAt { get; private set; }

    public static CaptureInputManifest Create(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid captureAttemptId,
        Guid missionId,
        string manifestFingerprint,
        int totalFileCount,
        long totalSizeBytes,
        DateTimeOffset createdAt,
        string? gpsMetadataSummary = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(captureAttemptId, nameof(captureAttemptId));
        DomainGuard.NotEmpty(missionId, nameof(missionId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var normalizedFingerprint = PolicyValidationGuard.NormalizeRequired(
            manifestFingerprint, 128, nameof(manifestFingerprint));

        if (totalFileCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalFileCount),
                totalFileCount,
                "Total file count must be positive.");
        }

        if (totalSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalSizeBytes),
                totalSizeBytes,
                "Total size bytes must be positive.");
        }

        var normalizedGps = PolicyValidationGuard.NormalizeOptional(
            gpsMetadataSummary, 500, nameof(gpsMetadataSummary));

        return new CaptureInputManifest
        {
            Id = id,
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            CaptureAttemptId = captureAttemptId,
            MissionId = missionId,
            ManifestFingerprint = normalizedFingerprint,
            TotalFileCount = totalFileCount,
            TotalSizeBytes = totalSizeBytes,
            GpsMetadataSummary = normalizedGps,
            Status = CaptureInputManifestStatus.Uploaded,
            CreatedAt = createdAt
        };
    }

    public void MarkApproved(DateTimeOffset approvedAt)
    {
        DomainGuard.Utc(approvedAt, nameof(approvedAt));
        if (Status != CaptureInputManifestStatus.Uploaded)
        {
            throw new InvalidOperationException($"Cannot approve manifest in status {Status}.");
        }
        Status = CaptureInputManifestStatus.Approved;
        EvaluatedAt = approvedAt;
    }

    public void MarkRejected(DateTimeOffset rejectedAt)
    {
        DomainGuard.Utc(rejectedAt, nameof(rejectedAt));
        if (Status != CaptureInputManifestStatus.Uploaded)
        {
            throw new InvalidOperationException($"Cannot reject manifest in status {Status}.");
        }
        Status = CaptureInputManifestStatus.Rejected;
        EvaluatedAt = rejectedAt;
    }

    public void MarkSuperseded(DateTimeOffset supersededAt)
    {
        DomainGuard.Utc(supersededAt, nameof(supersededAt));
        if (Status == CaptureInputManifestStatus.Approved)
        {
            throw new InvalidOperationException("Approved manifest cannot be superseded.");
        }
        Status = CaptureInputManifestStatus.Superseded;
        EvaluatedAt = supersededAt;
    }
}
