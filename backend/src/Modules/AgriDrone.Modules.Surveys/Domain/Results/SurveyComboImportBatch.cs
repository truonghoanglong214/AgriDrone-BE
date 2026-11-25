using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed record ComboPendingBlockingItem(
    string SourceType,
    string SourceId,
    string ReasonCode,
    string? Description = null);

public sealed class SurveyComboImportBatch : Entity
{
    public const string StatusPendingReview = "PENDING_REVIEW";
    public const string StatusReviewed = "REVIEWED";
    public const string StatusSuperseded = "SUPERSEDED";

    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private SurveyComboImportBatch() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid CaptureAttemptId { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid JobId { get; private set; }
    public int SourceRevision { get; private set; }
    public Guid HandoffId { get; private set; }
    public Guid? PredecessorBatchId { get; private set; }
    public Guid? SupersedesHandoffId { get; private set; }
    public Guid BusinessPolicyVersionId { get; private set; }
    public Guid? InputApprovalId { get; private set; }
    public Guid? InputManifestId { get; private set; }
    public string? InputManifestHash { get; private set; }
    public string MappingKind { get; private set; } = null!;
    public Guid MappingCandidateBatchId { get; private set; }
    public Guid FarmBaseMapVersionId { get; private set; }
    public Guid BaselineMapVersionId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public JsonDocument RawPayload { get; private set; } = null!;
    public string CanonicalFingerprint { get; private set; } = null!;
    public DateTimeOffset ProducedAt { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }
    public int BlockingCount { get; private set; }
    public JsonDocument BlockingItems { get; private set; } = null!;
    public string Status { get; private set; } = StatusPendingReview;

    public static Result<SurveyComboImportBatch> Create(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid captureAttemptId,
        Guid missionId,
        Guid jobId,
        int sourceRevision,
        Guid handoffId,
        Guid businessPolicyVersionId,
        string mappingKind,
        Guid mappingCandidateBatchId,
        Guid farmBaseMapVersionId,
        Guid baselineMapVersionId,
        Guid farmBoundaryVersionId,
        JsonDocument rawPayload,
        string canonicalFingerprint,
        DateTimeOffset producedAt,
        DateTimeOffset importedAt,
        int blockingCount,
        JsonDocument? blockingItems = null,
        Guid? predecessorBatchId = null,
        Guid? supersedesHandoffId = null,
        Guid? inputApprovalId = null,
        Guid? inputManifestId = null,
        string? inputManifestHash = null)
    {
        if (id == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("Id.Required", "Batch identifier is required."));
        if (tenantId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("TenantId.Required", "Tenant identifier is required."));
        if (farmId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("FarmId.Required", "Farm identifier is required."));
        if (surveyOrderId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("SurveyOrderId.Required", "Survey order identifier is required."));
        if (captureAttemptId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("CaptureAttemptId.Required", "Capture attempt identifier is required."));
        if (missionId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("MissionId.Required", "Mission identifier is required."));
        if (jobId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("JobId.Required", "AI job identifier is required."));
        if (sourceRevision < 1)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("SourceRevision.Invalid", "Source revision must be >= 1."));
        if (handoffId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("HandoffId.Required", "Handoff identifier is required."));
        if (businessPolicyVersionId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("BusinessPolicyVersionId.Required", "Business policy version identifier is required."));
        if (string.IsNullOrWhiteSpace(mappingKind))
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("MappingKind.Required", "Mapping kind is required."));
        if (mappingCandidateBatchId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("MappingCandidateBatchId.Required", "Mapping candidate batch identifier is required."));
        if (farmBaseMapVersionId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("FarmBaseMapVersionId.Required", "Base map version identifier is required."));
        if (baselineMapVersionId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("BaselineMapVersionId.Required", "Baseline map version identifier is required."));
        if (farmBoundaryVersionId == Guid.Empty)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("FarmBoundaryVersionId.Required", "Boundary version identifier is required."));
        if (rawPayload is null)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("RawPayload.Required", "Raw payload is required."));
        if (string.IsNullOrWhiteSpace(canonicalFingerprint) || !Hex64Regex.IsMatch(canonicalFingerprint))
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("CanonicalFingerprint.Invalid", "Canonical fingerprint must be a 64-character lowercase hex string."));
        if (producedAt == default || producedAt.Offset != TimeSpan.Zero)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("ProducedAt.Invalid", "ProducedAt must be a valid UTC timestamp."));
        if (importedAt == default || importedAt.Offset != TimeSpan.Zero)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("ImportedAt.Invalid", "ImportedAt must be a valid UTC timestamp."));
        if (blockingCount < 0)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("BlockingCount.Invalid", "Blocking count cannot be negative."));

        if (predecessorBatchId.HasValue && predecessorBatchId.Value == id)
            return Result.Failure<SurveyComboImportBatch>(AppError.Validation("PredecessorBatchId.SelfReference", "Predecessor batch cannot reference self."));

        return Result.Success(new SurveyComboImportBatch
        {
            Id = id,
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            CaptureAttemptId = captureAttemptId,
            MissionId = missionId,
            JobId = jobId,
            SourceRevision = sourceRevision,
            HandoffId = handoffId,
            PredecessorBatchId = predecessorBatchId,
            SupersedesHandoffId = supersedesHandoffId,
            BusinessPolicyVersionId = businessPolicyVersionId,
            InputApprovalId = inputApprovalId,
            InputManifestId = inputManifestId,
            InputManifestHash = inputManifestHash?.Trim(),
            MappingKind = mappingKind.Trim(),
            MappingCandidateBatchId = mappingCandidateBatchId,
            FarmBaseMapVersionId = farmBaseMapVersionId,
            BaselineMapVersionId = baselineMapVersionId,
            FarmBoundaryVersionId = farmBoundaryVersionId,
            RawPayload = rawPayload,
            CanonicalFingerprint = canonicalFingerprint,
            ProducedAt = producedAt,
            ImportedAt = importedAt,
            BlockingCount = blockingCount,
            BlockingItems = blockingItems ?? JsonDocument.Parse("[]"),
            Status = StatusPendingReview
        });
    }

    public void MarkReviewed()
    {
        Status = StatusReviewed;
    }

    public void MarkSuperseded()
    {
        Status = StatusSuperseded;
    }
}
