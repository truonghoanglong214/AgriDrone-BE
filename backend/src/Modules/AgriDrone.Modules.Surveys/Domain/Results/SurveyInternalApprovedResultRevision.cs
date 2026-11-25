using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyInternalApprovedResultRevision : Entity
{
    public const string ServiceMap = "MAP";
    public const string ServiceCombo = "COMBO";
    public const string HealthNotAssessed = "NotAssessed";
    public const string HealthSuspectedObservationsApproved = "SuspectedObservationsApproved";
    public const string HealthNoObservationsDetected = "NoObservationsDetected";

    public const string DecisionApproved = "Approved";
    public const string DecisionApprovedWithObservations = "ApprovedWithObservations";
    public const string DecisionInternalApproved = "InternalApproved";

    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    public static DateTimeOffset NormalizeUtcToMicroseconds(DateTimeOffset dt)
    {
        var utc = dt.ToUniversalTime();
        var microsecondTicks = (utc.Ticks / 10L) * 10L;
        return new DateTimeOffset(microsecondTicks, TimeSpan.Zero);
    }

    public static string CanonicalizeServiceCode(string? serviceCode)
    {
        var normalized = serviceCode?.Trim().ToUpperInvariant();
        return normalized switch
        {
            ServiceMap => ServiceMap,
            ServiceCombo => ServiceCombo,
            _ => normalized ?? string.Empty
        };
    }

    public static string CanonicalizeHealthAssessmentStatus(string? healthStatus)
    {
        var raw = healthStatus?.Trim();
        if (string.Equals(raw, HealthNotAssessed, StringComparison.OrdinalIgnoreCase))
            return HealthNotAssessed;
        if (string.Equals(raw, HealthSuspectedObservationsApproved, StringComparison.OrdinalIgnoreCase))
            return HealthSuspectedObservationsApproved;
        if (string.Equals(raw, HealthNoObservationsDetected, StringComparison.OrdinalIgnoreCase))
            return HealthNoObservationsDetected;
        return raw ?? string.Empty;
    }

    public static string CanonicalizeReviewDecision(string? decision)
    {
        var raw = decision?.Trim();
        if (string.Equals(raw, DecisionApproved, StringComparison.OrdinalIgnoreCase))
            return DecisionApproved;
        if (string.Equals(raw, DecisionApprovedWithObservations, StringComparison.OrdinalIgnoreCase))
            return DecisionApprovedWithObservations;
        if (string.Equals(raw, DecisionInternalApproved, StringComparison.OrdinalIgnoreCase))
            return DecisionInternalApproved;
        return raw ?? string.Empty;
    }

    public static string? CanonicalizeLimitationNotice(string? limitationNotice)
    {
        return string.IsNullOrWhiteSpace(limitationNotice) ? null : limitationNotice.Trim();
    }

    public static string CanonicalizeReviewReason(string? reviewReason)
    {
        return reviewReason?.Trim() ?? string.Empty;
    }

    private SurveyInternalApprovedResultRevision() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public int RevisionNumber { get; private set; }
    public Guid? PredecessorRevisionId { get; private set; }
    public string ServiceCode { get; private set; } = null!;
    public bool ScreensDisease { get; private set; }
    public Guid CaptureAttemptId { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid FarmBaseMapVersionId { get; private set; }
    public Guid BaselineMapVersionId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public Guid PoleCountSnapshotId { get; private set; }
    public Guid MappingCandidateBatchId { get; private set; }
    public Guid? ComboBatchId { get; private set; }
    public Guid? ComboReviewRevisionId { get; private set; }
    public string? ComboReviewFingerprint { get; private set; }
    public string ReviewDecision { get; private set; } = null!;
    public string HealthAssessmentStatus { get; private set; } = null!;
    public string? LimitationNotice { get; private set; }
    public Guid ReviewedBy { get; private set; }
    public DateTimeOffset ReviewedAt { get; private set; }
    public string ReviewReason { get; private set; } = null!;
    public string EvidenceFingerprint { get; private set; } = null!;
    public bool IsCurrent { get; private set; }
    public Guid? SupersededBy { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public string? SupersededReason { get; private set; }
    public Guid? SuccessorRevisionId { get; private set; }

    public static string ComputeEvidenceFingerprint(
        Guid tenantId,
        Guid surveyOrderId,
        Guid farmId,
        int revisionNumber,
        string serviceCode,
        bool screensDisease,
        Guid captureAttemptId,
        Guid missionId,
        Guid farmBaseMapVersionId,
        Guid baselineMapVersionId,
        Guid farmBoundaryVersionId,
        Guid poleCountSnapshotId,
        Guid mappingCandidateBatchId,
        Guid? comboBatchId,
        string reviewDecision,
        string healthAssessmentStatus,
        string? limitationNotice,
        Guid reviewedBy,
        DateTimeOffset reviewedAt,
        string reviewReason,
        Guid? predecessorRevisionId,
        Guid? comboReviewRevisionId = null,
        string? comboReviewFingerprint = null)
    {
        var normalizedReviewedAt = NormalizeUtcToMicroseconds(reviewedAt);
        var canonicalServiceCode = CanonicalizeServiceCode(serviceCode);
        var canonicalHealthStatus = CanonicalizeHealthAssessmentStatus(healthAssessmentStatus);
        var canonicalDecision = CanonicalizeReviewDecision(reviewDecision);
        var canonicalLimitation = CanonicalizeLimitationNotice(limitationNotice);
        var canonicalReason = CanonicalizeReviewReason(reviewReason);
        var normalizedComboReviewFingerprint = string.IsNullOrWhiteSpace(comboReviewFingerprint)
            ? null
            : comboReviewFingerprint.Trim().ToLowerInvariant();

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("tenant_id", tenantId.ToString("D"));
            writer.WriteString("survey_order_id", surveyOrderId.ToString("D"));
            writer.WriteString("farm_id", farmId.ToString("D"));
            writer.WriteNumber("revision_number", revisionNumber);
            if (predecessorRevisionId.HasValue)
                writer.WriteString("predecessor_revision_id", predecessorRevisionId.Value.ToString("D"));
            else
                writer.WriteNull("predecessor_revision_id");
            writer.WriteString("service_code", canonicalServiceCode);
            writer.WriteBoolean("screens_disease", screensDisease);
            writer.WriteString("capture_attempt_id", captureAttemptId.ToString("D"));
            writer.WriteString("mission_id", missionId.ToString("D"));
            writer.WriteString("farm_base_map_version_id", farmBaseMapVersionId.ToString("D"));
            writer.WriteString("baseline_map_version_id", baselineMapVersionId.ToString("D"));
            writer.WriteString("farm_boundary_version_id", farmBoundaryVersionId.ToString("D"));
            writer.WriteString("pole_count_snapshot_id", poleCountSnapshotId.ToString("D"));
            writer.WriteString("mapping_candidate_batch_id", mappingCandidateBatchId.ToString("D"));
            if (comboBatchId.HasValue)
                writer.WriteString("combo_batch_id", comboBatchId.Value.ToString("D"));
            else
                writer.WriteNull("combo_batch_id");
            writer.WriteString("review_decision", canonicalDecision);
            writer.WriteString("health_assessment_status", canonicalHealthStatus);
            if (canonicalLimitation is not null)
                writer.WriteString("limitation_notice", canonicalLimitation);
            else
                writer.WriteNull("limitation_notice");
            writer.WriteString("reviewed_by", reviewedBy.ToString("D"));
            writer.WriteString("reviewed_at", normalizedReviewedAt.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
            writer.WriteString("review_reason", canonicalReason);
            if (comboReviewRevisionId.HasValue || normalizedComboReviewFingerprint is not null)
            {
                if (comboReviewRevisionId.HasValue)
                    writer.WriteString("combo_review_revision_id", comboReviewRevisionId.Value.ToString("D"));
                else
                    writer.WriteNull("combo_review_revision_id");
                if (normalizedComboReviewFingerprint is not null)
                    writer.WriteString("combo_review_fingerprint", normalizedComboReviewFingerprint);
                else
                    writer.WriteNull("combo_review_fingerprint");
            }
            writer.WriteEndObject();
        }

        var hash = SHA256.HashData(stream.ToArray());
        return Convert.ToHexStringLower(hash);
    }

    public static Result<SurveyInternalApprovedResultRevision> Create(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        int revisionNumber,
        string serviceCode,
        bool screensDisease,
        Guid captureAttemptId,
        Guid missionId,
        Guid farmBaseMapVersionId,
        Guid baselineMapVersionId,
        Guid farmBoundaryVersionId,
        Guid poleCountSnapshotId,
        Guid mappingCandidateBatchId,
        Guid? comboBatchId,
        string reviewDecision,
        string healthAssessmentStatus,
        string? limitationNotice,
        Guid reviewedBy,
        DateTimeOffset reviewedAt,
        string reviewReason,
        string evidenceFingerprint,
        Guid? predecessorRevisionId = null,
        Guid? comboReviewRevisionId = null,
        string? comboReviewFingerprint = null)
    {
        if (id == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("Id.Required", "Identifier is required."));
        if (tenantId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("TenantId.Required", "Tenant identifier is required."));
        if (farmId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("FarmId.Required", "Farm identifier is required."));
        if (surveyOrderId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("SurveyOrderId.Required", "Survey order identifier is required."));
        if (revisionNumber < 1) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("RevisionNumber.Invalid", "Revision number must be >= 1."));

        if (revisionNumber == 1 && predecessorRevisionId.HasValue)
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("PredecessorRevisionId.Invalid", "Initial revision cannot have a predecessor."));
        if (revisionNumber > 1 && (!predecessorRevisionId.HasValue || predecessorRevisionId.Value == Guid.Empty || predecessorRevisionId.Value == id))
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("PredecessorRevisionId.Required", "Subsequent revision must reference a valid distinct predecessor."));

        if (captureAttemptId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("CaptureAttemptId.Required", "Capture attempt identifier is required."));
        if (missionId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("MissionId.Required", "Mission identifier is required."));
        if (farmBaseMapVersionId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("FarmBaseMapVersionId.Required", "Base map identifier is required."));
        if (baselineMapVersionId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("BaselineMapVersionId.Required", "Baseline map identifier is required."));
        if (farmBoundaryVersionId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("FarmBoundaryVersionId.Required", "Boundary identifier is required."));
        if (poleCountSnapshotId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("PoleCountSnapshotId.Required", "Pole count snapshot identifier is required."));
        if (mappingCandidateBatchId == Guid.Empty) return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("MappingCandidateBatchId.Required", "Mapping candidate batch identifier is required."));

        // Canonicalize inputs
        var normalizedServiceCode = CanonicalizeServiceCode(serviceCode);
        if (normalizedServiceCode is not (ServiceMap or ServiceCombo))
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ServiceCode.Invalid", "ServiceCode must be either MAP or COMBO."));

        var canonicalHealthStatus = CanonicalizeHealthAssessmentStatus(healthAssessmentStatus);
        if (canonicalHealthStatus is not (HealthNotAssessed or HealthSuspectedObservationsApproved or HealthNoObservationsDetected))
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("HealthAssessmentStatus.Invalid", $"Health assessment status '{healthAssessmentStatus}' is unsupported."));

        var normalizedLimitation = CanonicalizeLimitationNotice(limitationNotice);
        if (normalizedLimitation is not null && normalizedLimitation.Length > 1000)
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("LimitationNotice.TooLong", "Limitation notice must not exceed 1000 characters."));

        var normalizedComboReviewFingerprint = string.IsNullOrWhiteSpace(comboReviewFingerprint)
            ? null
            : comboReviewFingerprint.Trim().ToLowerInvariant();

        // Validate MAP vs COMBO rules
        if (normalizedServiceCode == ServiceMap)
        {
            if (screensDisease)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ScreensDisease.Invalid", "MAP service cannot screen disease."));
            if (canonicalHealthStatus != HealthNotAssessed)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("HealthAssessmentStatus.Invalid", "MAP service health assessment status must be NotAssessed."));
            if (comboBatchId.HasValue)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ComboBatchId.Invalid", "MAP service cannot reference a combo analysis batch."));
            if (comboReviewRevisionId.HasValue || normalizedComboReviewFingerprint is not null)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ComboReviewRevisionId.Invalid", "MAP service cannot reference a COMBO review revision."));
            if (normalizedLimitation is not null)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("LimitationNotice.Invalid", "MAP service cannot have a limitation notice."));
        }
        else // COMBO
        {
            if (!screensDisease)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ScreensDisease.Required", "COMBO service must screen disease."));
            if (canonicalHealthStatus is not (HealthSuspectedObservationsApproved or HealthNoObservationsDetected))
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("HealthAssessmentStatus.Invalid", "COMBO service health status must be SuspectedObservationsApproved or NoObservationsDetected."));
            if (!comboBatchId.HasValue || comboBatchId.Value == Guid.Empty)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ComboBatchId.Required", "COMBO service requires a valid ComboBatchId."));
            if (!comboReviewRevisionId.HasValue || comboReviewRevisionId.Value == Guid.Empty)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ComboReviewRevisionId.Required", "COMBO service requires a valid ComboReviewRevisionId."));
            if (normalizedComboReviewFingerprint is null || !Hex64Regex.IsMatch(normalizedComboReviewFingerprint))
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ComboReviewFingerprint.Invalid", "COMBO service requires a valid 64-character lowercase hex ComboReviewFingerprint."));
            if (normalizedLimitation is null)
                return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("LimitationNotice.Required", "COMBO service requires an explicit limitation notice (cannot certify disease-free)."));
        }

        // Validate approved decision set
        var canonicalDecision = CanonicalizeReviewDecision(reviewDecision);
        if (canonicalDecision is not (DecisionApproved or DecisionApprovedWithObservations or DecisionInternalApproved))
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ReviewDecision.Invalid", "ReviewDecision must have approval semantics (Approved, ApprovedWithObservations, or InternalApproved)."));

        var normalizedReason = CanonicalizeReviewReason(reviewReason);
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ReviewReason.Invalid", "ReviewReason must be between 1 and 500 characters."));

        if (reviewedBy == Guid.Empty)
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ReviewedBy.Required", "Reviewer identifier is required."));
        if (reviewedAt == default || reviewedAt.Offset != TimeSpan.Zero)
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("ReviewedAt.Invalid", "ReviewedAt must be a valid UTC timestamp."));

        var normalizedReviewedAt = NormalizeUtcToMicroseconds(reviewedAt);

        if (string.IsNullOrWhiteSpace(evidenceFingerprint) || !Hex64Regex.IsMatch(evidenceFingerprint))
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("EvidenceFingerprint.Invalid", "Evidence fingerprint must be a 64-character lowercase hex string."));

        var expectedFingerprint = ComputeEvidenceFingerprint(
            tenantId, surveyOrderId, farmId, revisionNumber, normalizedServiceCode, screensDisease,
            captureAttemptId, missionId, farmBaseMapVersionId, baselineMapVersionId,
            farmBoundaryVersionId, poleCountSnapshotId, mappingCandidateBatchId,
            comboBatchId, canonicalDecision, canonicalHealthStatus, normalizedLimitation,
            reviewedBy, normalizedReviewedAt, normalizedReason, predecessorRevisionId,
            comboReviewRevisionId, normalizedComboReviewFingerprint);

        if (!string.Equals(evidenceFingerprint, expectedFingerprint, StringComparison.Ordinal))
            return Result.Failure<SurveyInternalApprovedResultRevision>(AppError.Validation("EvidenceFingerprint.Mismatch", "Evidence fingerprint does not match computed immutable token."));

        return Result.Success(new SurveyInternalApprovedResultRevision
        {
            Id = id,
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            RevisionNumber = revisionNumber,
            PredecessorRevisionId = predecessorRevisionId,
            ServiceCode = normalizedServiceCode,
            ScreensDisease = screensDisease,
            CaptureAttemptId = captureAttemptId,
            MissionId = missionId,
            FarmBaseMapVersionId = farmBaseMapVersionId,
            BaselineMapVersionId = baselineMapVersionId,
            FarmBoundaryVersionId = farmBoundaryVersionId,
            PoleCountSnapshotId = poleCountSnapshotId,
            MappingCandidateBatchId = mappingCandidateBatchId,
            ComboBatchId = comboBatchId,
            ComboReviewRevisionId = comboReviewRevisionId,
            ComboReviewFingerprint = normalizedComboReviewFingerprint,
            ReviewDecision = canonicalDecision,
            HealthAssessmentStatus = canonicalHealthStatus,
            LimitationNotice = normalizedLimitation,
            ReviewedBy = reviewedBy,
            ReviewedAt = normalizedReviewedAt,
            ReviewReason = normalizedReason,
            EvidenceFingerprint = evidenceFingerprint,
            IsCurrent = true
        });
    }

    public Result Supersede(Guid actorId, DateTimeOffset supersededAt, string reason, Guid successorRevisionId)
    {
        if (actorId == Guid.Empty)
            return Result.Failure(AppError.Validation("ActorId.Required", "Actor identifier is required to supersede revision."));
        if (supersededAt == default || supersededAt.Offset != TimeSpan.Zero)
            return Result.Failure(AppError.Validation("SupersededAt.Invalid", "SupersededAt must be a valid UTC timestamp."));

        var normalizedSupersededAt = NormalizeUtcToMicroseconds(supersededAt);
        if (normalizedSupersededAt < ReviewedAt)
            return Result.Failure(AppError.Validation("SupersededAt.BeforeReviewedAt", "SupersededAt cannot be earlier than ReviewedAt."));

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)
            return Result.Failure(AppError.Validation("Reason.Invalid", "Superseded reason must be between 1 and 500 characters."));

        if (successorRevisionId == Guid.Empty || successorRevisionId == Id)
            return Result.Failure(AppError.Validation("SuccessorRevisionId.Invalid", "Successor revision ID must be a valid distinct identifier."));

        if (!IsCurrent)
        {
            // Idempotent replay check
            if (SupersededBy == actorId &&
                SupersededAt == normalizedSupersededAt &&
                string.Equals(SupersededReason, normalizedReason, StringComparison.Ordinal) &&
                SuccessorRevisionId == successorRevisionId)
            {
                return Result.Success();
            }

            return Result.Failure(AppError.Conflict(
                "Revision.AlreadySuperseded",
                "Internal approved result revision has already been superseded with different audit metadata."));
        }

        IsCurrent = false;
        SupersededBy = actorId;
        SupersededAt = normalizedSupersededAt;
        SupersededReason = normalizedReason;
        SuccessorRevisionId = successorRevisionId;
        return Result.Success();
    }
}
