using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class CaptureInputApproval : Entity
{
    private CaptureInputApproval() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid CaptureAttemptId { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid InputManifestId { get; private set; }
    public string ManifestFingerprint { get; private set; } = null!;
    public Guid ProcessingJobId { get; private set; }
    public Guid ApprovedBy { get; private set; }
    public DateTimeOffset ApprovedAt { get; private set; }
    public string? TechnicalQualityNotes { get; private set; }

    public static CaptureInputApproval Create(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid captureAttemptId,
        Guid missionId,
        Guid inputManifestId,
        string manifestFingerprint,
        Guid processingJobId,
        Guid approvedBy,
        DateTimeOffset approvedAt,
        string? technicalQualityNotes = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(captureAttemptId, nameof(captureAttemptId));
        DomainGuard.NotEmpty(missionId, nameof(missionId));
        DomainGuard.NotEmpty(inputManifestId, nameof(inputManifestId));
        DomainGuard.NotEmpty(processingJobId, nameof(processingJobId));
        DomainGuard.NotEmpty(approvedBy, nameof(approvedBy));
        DomainGuard.Utc(approvedAt, nameof(approvedAt));

        var normalizedFingerprint = PolicyValidationGuard.NormalizeRequired(
            manifestFingerprint, 128, nameof(manifestFingerprint));
        var normalizedNotes = PolicyValidationGuard.NormalizeOptional(
            technicalQualityNotes, 500, nameof(technicalQualityNotes));

        return new CaptureInputApproval
        {
            Id = id,
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            CaptureAttemptId = captureAttemptId,
            MissionId = missionId,
            InputManifestId = inputManifestId,
            ManifestFingerprint = normalizedFingerprint,
            ProcessingJobId = processingJobId,
            ApprovedBy = approvedBy,
            ApprovedAt = approvedAt,
            TechnicalQualityNotes = normalizedNotes
        };
    }
}
