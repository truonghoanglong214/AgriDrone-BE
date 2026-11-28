using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed record ComboFindingReviewDecisionRecord(
    Guid FindingId,
    string Decision,
    string? Reason,
    IReadOnlyList<Guid> EvidenceMediaIds,
    Guid? ReviewedPlantProfileId = null);

public sealed record ComboZoneReviewDecisionRecord(
    string SourceProposalId,
    string Decision,
    GeoJsonPolygonV3? ReviewedGeometry,
    IReadOnlyList<Guid>? ReviewedFindingIds,
    Guid? SelectedGuidanceId,
    int? SelectedGuidanceVersionNumber,
    string? Reason,
    IReadOnlyList<Guid> EvidenceMediaIds);

public sealed record ComboBlockerResolutionRecord(
    string SourceType,
    string SourceId,
    string ReasonCode,
    string Resolution,
    string? Reason,
    IReadOnlyList<Guid> EvidenceMediaIds);

public sealed record ComboReviewDecisionsSnapshotRecord(
    IReadOnlyList<ComboFindingReviewDecisionRecord> FindingDecisions,
    IReadOnlyList<ComboZoneReviewDecisionRecord> ZoneDecisions,
    IReadOnlyList<ComboBlockerResolutionRecord> BlockerResolutions);

public sealed class SurveyComboReviewRevision : Entity
{
    public const string OutcomeApproved = "Approved";
    public const string OutcomeRejected = "Rejected";

    public const string FindingDecisionAccept = "Accept";
    public const string FindingDecisionExclude = "Exclude";

    public const string ZoneDecisionApprove = "Approve";
    public const string ZoneDecisionReject = "Reject";
    public const string ZoneDecisionAdjust = "Adjust";

    public const string BlockerResolutionResolved = "Resolved";
    public const string BlockerResolutionAcceptedWithJustification = "AcceptedWithJustification";
    public const string BlockerResolutionExcluded = "Excluded";
    public const string BlockerResolutionCorrected = "Corrected";
    public const string BlockerResolutionUnresolved = "Unresolved";

    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private sealed record CanonicalFindingDecisionForFingerprint(
        Guid FindingId,
        string Decision,
        string? Reason,
        IReadOnlyList<Guid> EvidenceMediaIds,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        Guid? ReviewedPlantProfileId);

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private SurveyComboReviewRevision() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid CaptureAttemptId { get; private set; }
    public Guid ComboBatchId { get; private set; }
    public int SourceRevision { get; private set; }
    public string BatchFingerprint { get; private set; } = null!;
    public int RevisionNumber { get; private set; }
    public Guid? PredecessorReviewId { get; private set; }
    public string Outcome { get; private set; } = null!;
    public string ReviewReason { get; private set; } = null!;
    public bool ConfirmNoDiseaseObserved { get; private set; }
    public string? NegativeResultConfirmationEvidence { get; private set; }
    public Guid ReviewedBy { get; private set; }
    public DateTimeOffset ReviewedAt { get; private set; }
    public int FindingsCount { get; private set; }
    public int AcceptedFindingsCount { get; private set; }
    public int ExcludedFindingsCount { get; private set; }
    public int ZonesCount { get; private set; }
    public int ApprovedZonesCount { get; private set; }
    public int RejectedZonesCount { get; private set; }
    public int BlockersCount { get; private set; }
    public int ResolvedBlockersCount { get; private set; }
    public int UnresolvedBlockersCount { get; private set; }
    public JsonDocument DecisionsPayload { get; private set; } = null!;
    public string DecisionFingerprint { get; private set; } = null!;
    public bool IsCurrent { get; private set; }
    public Guid? SupersededBy { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public string? SupersededReason { get; private set; }
    public Guid? SuccessorReviewId { get; private set; }

    public static DateTimeOffset NormalizeUtcToMicroseconds(DateTimeOffset dt) =>
        SurveyInternalApprovedResultRevision.NormalizeUtcToMicroseconds(dt);

    public static string CanonicalizeOutcome(string? outcome)
    {
        var raw = outcome?.Trim();
        if (string.Equals(raw, OutcomeApproved, StringComparison.OrdinalIgnoreCase))
            return OutcomeApproved;
        if (string.Equals(raw, OutcomeRejected, StringComparison.OrdinalIgnoreCase))
            return OutcomeRejected;
        return raw ?? string.Empty;
    }

    public static ComboReviewDecisionsSnapshotRecord CanonicalizeDecisions(ComboReviewDecisionsSnapshotRecord decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var findingDecisions = decisions.FindingDecisions
            .OrderBy(f => f.FindingId)
            .Select(f => f with
            {
                Decision = CanonicalizeFindingDecision(f.Decision),
                Reason = string.IsNullOrWhiteSpace(f.Reason) ? null : f.Reason.Trim(),
                EvidenceMediaIds = (f.EvidenceMediaIds ?? []).Distinct().Order().ToArray()
            })
            .ToArray();

        var zoneDecisions = decisions.ZoneDecisions
            .OrderBy(z => z.SourceProposalId.Trim(), StringComparer.Ordinal)
            .Select(z => z with
            {
                SourceProposalId = z.SourceProposalId.Trim(),
                Decision = CanonicalizeZoneDecision(z.Decision),
                ReviewedFindingIds = z.ReviewedFindingIds is null ? null : z.ReviewedFindingIds.Distinct().Order().ToArray(),
                Reason = string.IsNullOrWhiteSpace(z.Reason) ? null : z.Reason.Trim(),
                EvidenceMediaIds = (z.EvidenceMediaIds ?? []).Distinct().Order().ToArray()
            })
            .ToArray();

        var blockerResolutions = decisions.BlockerResolutions
            .Select(b => b with
            {
                SourceType = b.SourceType.Trim(),
                SourceId = b.SourceId.Trim(),
                ReasonCode = b.ReasonCode.Trim(),
                Resolution = CanonicalizeBlockerResolution(b.Resolution),
                Reason = string.IsNullOrWhiteSpace(b.Reason) ? null : b.Reason.Trim(),
                EvidenceMediaIds = (b.EvidenceMediaIds ?? []).Distinct().Order().ToArray()
            })
            .OrderBy(b => b.SourceType, StringComparer.Ordinal)
            .ThenBy(b => b.SourceId, StringComparer.Ordinal)
            .ThenBy(b => b.ReasonCode, StringComparer.Ordinal)
            .ToArray();

        return new ComboReviewDecisionsSnapshotRecord(findingDecisions, zoneDecisions, blockerResolutions);
    }

    public static string CanonicalizeFindingDecision(string? decision)
    {
        var raw = decision?.Trim();
        if (string.Equals(raw, FindingDecisionAccept, StringComparison.OrdinalIgnoreCase))
            return FindingDecisionAccept;
        if (string.Equals(raw, FindingDecisionExclude, StringComparison.OrdinalIgnoreCase))
            return FindingDecisionExclude;
        return raw ?? string.Empty;
    }

    public static string CanonicalizeZoneDecision(string? decision)
    {
        var raw = decision?.Trim();
        if (string.Equals(raw, ZoneDecisionApprove, StringComparison.OrdinalIgnoreCase))
            return ZoneDecisionApprove;
        if (string.Equals(raw, ZoneDecisionReject, StringComparison.OrdinalIgnoreCase))
            return ZoneDecisionReject;
        if (string.Equals(raw, ZoneDecisionAdjust, StringComparison.OrdinalIgnoreCase))
            return ZoneDecisionAdjust;
        return raw ?? string.Empty;
    }

    public static string CanonicalizeBlockerResolution(string? resolution)
    {
        var raw = resolution?.Trim();
        if (string.Equals(raw, BlockerResolutionResolved, StringComparison.OrdinalIgnoreCase))
            return BlockerResolutionResolved;
        if (string.Equals(raw, BlockerResolutionAcceptedWithJustification, StringComparison.OrdinalIgnoreCase))
            return BlockerResolutionAcceptedWithJustification;
        if (string.Equals(raw, BlockerResolutionExcluded, StringComparison.OrdinalIgnoreCase))
            return BlockerResolutionExcluded;
        if (string.Equals(raw, BlockerResolutionCorrected, StringComparison.OrdinalIgnoreCase))
            return BlockerResolutionCorrected;
        if (string.Equals(raw, BlockerResolutionUnresolved, StringComparison.OrdinalIgnoreCase))
            return BlockerResolutionUnresolved;
        return raw ?? string.Empty;
    }

    public static bool IsResolvedBlockerStatus(string canonicalResolution) =>
        canonicalResolution is BlockerResolutionResolved
            or BlockerResolutionAcceptedWithJustification
            or BlockerResolutionExcluded
            or BlockerResolutionCorrected;

    public static string ComputeDecisionFingerprint(
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid captureAttemptId,
        Guid comboBatchId,
        int sourceRevision,
        string batchFingerprint,
        int revisionNumber,
        Guid? predecessorReviewId,
        string outcome,
        string reviewReason,
        bool confirmNoDiseaseObserved,
        string? negativeResultConfirmationEvidence,
        Guid reviewedBy,
        DateTimeOffset reviewedAt,
        ComboReviewDecisionsSnapshotRecord decisions)
    {
        var normalizedReviewedAt = NormalizeUtcToMicroseconds(reviewedAt);
        var normalizedOutcome = CanonicalizeOutcome(outcome);
        var normalizedReason = reviewReason?.Trim() ?? string.Empty;
        var normalizedNegativeEvidence = string.IsNullOrWhiteSpace(negativeResultConfirmationEvidence)
            ? null
            : negativeResultConfirmationEvidence.Trim();
        var canonicalDecisions = CanonicalizeDecisions(decisions);
        var fingerprintSnapshot = new
        {
            FindingDecisions = canonicalDecisions.FindingDecisions
                .Select(f => new CanonicalFindingDecisionForFingerprint(
                    f.FindingId,
                    f.Decision,
                    f.Reason,
                    f.EvidenceMediaIds,
                    f.ReviewedPlantProfileId))
                .ToArray(),
            canonicalDecisions.ZoneDecisions,
            canonicalDecisions.BlockerResolutions
        };
        var decisionsBytes = JsonSerializer.SerializeToUtf8Bytes(fingerprintSnapshot, CanonicalJsonOptions);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("tenant_id", tenantId.ToString("D"));
            writer.WriteString("farm_id", farmId.ToString("D"));
            writer.WriteString("survey_order_id", surveyOrderId.ToString("D"));
            writer.WriteString("capture_attempt_id", captureAttemptId.ToString("D"));
            writer.WriteString("combo_batch_id", comboBatchId.ToString("D"));
            writer.WriteNumber("source_revision", sourceRevision);
            writer.WriteString("batch_fingerprint", (batchFingerprint ?? string.Empty).Trim().ToLowerInvariant());
            writer.WriteNumber("revision_number", revisionNumber);
            if (predecessorReviewId.HasValue)
                writer.WriteString("predecessor_review_id", predecessorReviewId.Value.ToString("D"));
            else
                writer.WriteNull("predecessor_review_id");
            writer.WriteString("outcome", normalizedOutcome);
            writer.WriteString("review_reason", normalizedReason);
            writer.WriteBoolean("confirm_no_disease_observed", confirmNoDiseaseObserved);
            if (normalizedNegativeEvidence is not null)
                writer.WriteString("negative_result_confirmation_evidence", normalizedNegativeEvidence);
            else
                writer.WriteNull("negative_result_confirmation_evidence");
            writer.WriteString("reviewed_by", reviewedBy.ToString("D"));
            writer.WriteString("reviewed_at", normalizedReviewedAt.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
            writer.WritePropertyName("decisions");
            using (var doc = JsonDocument.Parse(decisionsBytes))
            {
                doc.RootElement.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    public static Result<SurveyComboReviewRevision> Create(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        Guid captureAttemptId,
        Guid comboBatchId,
        int sourceRevision,
        string batchFingerprint,
        int revisionNumber,
        Guid? predecessorReviewId,
        string outcome,
        string reviewReason,
        bool confirmNoDiseaseObserved,
        string? negativeResultConfirmationEvidence,
        Guid reviewedBy,
        DateTimeOffset reviewedAt,
        ComboReviewDecisionsSnapshotRecord decisions,
        string decisionFingerprint)
    {
        if (id == Guid.Empty) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("Id.Required", "Review identifier is required."));
        if (tenantId == Guid.Empty) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("TenantId.Required", "Tenant identifier is required."));
        if (farmId == Guid.Empty) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("FarmId.Required", "Farm identifier is required."));
        if (surveyOrderId == Guid.Empty) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("SurveyOrderId.Required", "Survey order identifier is required."));
        if (captureAttemptId == Guid.Empty) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("CaptureAttemptId.Required", "Capture attempt identifier is required."));
        if (comboBatchId == Guid.Empty) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("ComboBatchId.Required", "COMBO batch identifier is required."));
        if (sourceRevision < 1) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("SourceRevision.Invalid", "Source revision must be >= 1."));
        if (revisionNumber < 1) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("RevisionNumber.Invalid", "Review revision number must be >= 1."));

        if (revisionNumber == 1 && predecessorReviewId.HasValue)
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("PredecessorReviewId.Invalid", "Initial review revision cannot have a predecessor."));
        if (revisionNumber > 1 && (!predecessorReviewId.HasValue || predecessorReviewId.Value == Guid.Empty || predecessorReviewId.Value == id))
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("PredecessorReviewId.Required", "Subsequent review revision must reference a valid distinct predecessor."));

        var normalizedBatchFingerprint = batchFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Hex64Regex.IsMatch(normalizedBatchFingerprint))
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("BatchFingerprint.Invalid", "Batch fingerprint must be a 64-character lowercase hex string."));

        var canonicalOutcome = CanonicalizeOutcome(outcome);
        if (canonicalOutcome is not (OutcomeApproved or OutcomeRejected))
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("Outcome.Invalid", "Review outcome must be Approved or Rejected."));

        var normalizedReason = reviewReason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("ReviewReason.Invalid", "Review reason must be between 1 and 500 characters."));

        var normalizedNegativeEvidence = string.IsNullOrWhiteSpace(negativeResultConfirmationEvidence)
            ? null
            : negativeResultConfirmationEvidence.Trim();
        if (normalizedNegativeEvidence is not null && normalizedNegativeEvidence.Length > 500)
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("NegativeResultConfirmationEvidence.TooLong", "Negative confirmation evidence must not exceed 500 characters."));

        if (reviewedBy == Guid.Empty) return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("ReviewedBy.Required", "Reviewer identifier is required."));
        if (reviewedAt == default || reviewedAt.Offset != TimeSpan.Zero)
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("ReviewedAt.Invalid", "ReviewedAt must be a valid UTC timestamp."));
        if (decisions is null)
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("Decisions.Required", "Normalized review decisions are required."));

        var normalizedDecisions = CanonicalizeDecisions(decisions);

        int findingsCount = normalizedDecisions.FindingDecisions.Count;
        int acceptedFindingsCount = normalizedDecisions.FindingDecisions.Count(f => f.Decision == FindingDecisionAccept);
        int excludedFindingsCount = normalizedDecisions.FindingDecisions.Count(f => f.Decision == FindingDecisionExclude);
        int zonesCount = normalizedDecisions.ZoneDecisions.Count;
        int approvedZonesCount = normalizedDecisions.ZoneDecisions.Count(z => z.Decision is ZoneDecisionApprove or ZoneDecisionAdjust);
        int rejectedZonesCount = normalizedDecisions.ZoneDecisions.Count(z => z.Decision == ZoneDecisionReject);
        int blockersCount = normalizedDecisions.BlockerResolutions.Count;
        int resolvedBlockersCount = normalizedDecisions.BlockerResolutions.Count(b => IsResolvedBlockerStatus(b.Resolution));
        int unresolvedBlockersCount = blockersCount - resolvedBlockersCount;

        if (canonicalOutcome == OutcomeApproved && unresolvedBlockersCount > 0)
        {
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation(
                "ComboReview.UnresolvedBlockers",
                "Cannot mark COMBO review outcome as Approved while unresolved blocking items remain."));
        }

        var normalizedReviewedAt = NormalizeUtcToMicroseconds(reviewedAt);
        var normalizedFingerprint = decisionFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Hex64Regex.IsMatch(normalizedFingerprint))
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("DecisionFingerprint.Invalid", "Decision fingerprint must be a 64-character lowercase hex string."));

        var expectedFingerprint = ComputeDecisionFingerprint(
            tenantId, farmId, surveyOrderId, captureAttemptId, comboBatchId, sourceRevision,
            normalizedBatchFingerprint, revisionNumber, predecessorReviewId, canonicalOutcome,
            normalizedReason, confirmNoDiseaseObserved, normalizedNegativeEvidence,
            reviewedBy, normalizedReviewedAt, normalizedDecisions);

        if (!string.Equals(normalizedFingerprint, expectedFingerprint, StringComparison.Ordinal))
            return Result.Failure<SurveyComboReviewRevision>(AppError.Validation("DecisionFingerprint.Mismatch", "Decision fingerprint does not match computed canonical hash."));

        var payloadDoc = JsonSerializer.SerializeToDocument(normalizedDecisions, CanonicalJsonOptions);

        return Result.Success(new SurveyComboReviewRevision
        {
            Id = id,
            TenantId = tenantId,
            FarmId = farmId,
            SurveyOrderId = surveyOrderId,
            CaptureAttemptId = captureAttemptId,
            ComboBatchId = comboBatchId,
            SourceRevision = sourceRevision,
            BatchFingerprint = normalizedBatchFingerprint,
            RevisionNumber = revisionNumber,
            PredecessorReviewId = predecessorReviewId,
            Outcome = canonicalOutcome,
            ReviewReason = normalizedReason,
            ConfirmNoDiseaseObserved = confirmNoDiseaseObserved,
            NegativeResultConfirmationEvidence = normalizedNegativeEvidence,
            ReviewedBy = reviewedBy,
            ReviewedAt = normalizedReviewedAt,
            FindingsCount = findingsCount,
            AcceptedFindingsCount = acceptedFindingsCount,
            ExcludedFindingsCount = excludedFindingsCount,
            ZonesCount = zonesCount,
            ApprovedZonesCount = approvedZonesCount,
            RejectedZonesCount = rejectedZonesCount,
            BlockersCount = blockersCount,
            ResolvedBlockersCount = resolvedBlockersCount,
            UnresolvedBlockersCount = unresolvedBlockersCount,
            DecisionsPayload = payloadDoc,
            DecisionFingerprint = normalizedFingerprint,
            IsCurrent = true
        });
    }

    public Result<ComboReviewDecisionsSnapshotRecord> ReadDecisions()
    {
        try
        {
            var decoded = DecisionsPayload.Deserialize<ComboReviewDecisionsSnapshotRecord>(CanonicalJsonOptions);
            if (decoded is null)
            {
                return Result.Failure<ComboReviewDecisionsSnapshotRecord>(
                    AppError.Validation("DecisionsPayload.Null", "Persisted review decisions deserialized to null."));
            }
            return Result.Success(decoded);
        }
        catch (JsonException)
        {
            return Result.Failure<ComboReviewDecisionsSnapshotRecord>(
                AppError.Validation("DecisionsPayload.Corrupted", "Persisted review decisions JSON is invalid."));
        }
    }

    public Result Supersede(Guid actorId, DateTimeOffset supersededAt, string reason, Guid successorReviewId)
    {
        if (actorId == Guid.Empty)
            return Result.Failure(AppError.Validation("ActorId.Required", "Actor identifier is required to supersede review revision."));
        if (supersededAt == default || supersededAt.Offset != TimeSpan.Zero)
            return Result.Failure(AppError.Validation("SupersededAt.Invalid", "SupersededAt must be a valid UTC timestamp."));

        var normalizedSupersededAt = NormalizeUtcToMicroseconds(supersededAt);
        if (normalizedSupersededAt < ReviewedAt)
            return Result.Failure(AppError.Validation("SupersededAt.BeforeReviewedAt", "SupersededAt cannot be earlier than ReviewedAt."));

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)
            return Result.Failure(AppError.Validation("Reason.Invalid", "Superseded reason must be between 1 and 500 characters."));

        if (successorReviewId == Guid.Empty || successorReviewId == Id)
            return Result.Failure(AppError.Validation("SuccessorReviewId.Invalid", "Successor review ID must be a valid distinct identifier."));

        if (!IsCurrent)
        {
            if (SupersededBy == actorId &&
                SupersededAt == normalizedSupersededAt &&
                string.Equals(SupersededReason, normalizedReason, StringComparison.Ordinal) &&
                SuccessorReviewId == successorReviewId)
            {
                return Result.Success();
            }

            return Result.Failure(AppError.Conflict(
                "ReviewRevision.AlreadySuperseded",
                "COMBO review revision has already been superseded with different audit metadata."));
        }

        IsCurrent = false;
        SupersededBy = actorId;
        SupersededAt = normalizedSupersededAt;
        SupersededReason = normalizedReason;
        SuccessorReviewId = successorReviewId;
        return Result.Success();
    }
}
