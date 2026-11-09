using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyResult : AggregateRoot
{
    private static readonly Dictionary<SurveyResultStatus, IReadOnlySet<SurveyResultStatus>>
        AllowedTransitions = new Dictionary<SurveyResultStatus, IReadOnlySet<SurveyResultStatus>>
        {
            [SurveyResultStatus.PendingReview] = new HashSet<SurveyResultStatus>
            {
                SurveyResultStatus.Approved
            },
            [SurveyResultStatus.Approved] = new HashSet<SurveyResultStatus>
            {
                SurveyResultStatus.Published
            }
        };

    private SurveyResult() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public SurveyServiceType ServiceType { get; private set; }
    public SurveyResultStatus Status { get; private set; }
    public JsonDocument Provenance { get; private set; } = null!;
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public uint Version { get; private set; }
    public Guid? ActiveImportBatchId { get; private set; }
    public Guid? SourceMissionId { get; private set; }
    public Guid? FarmBoundaryVersionId { get; private set; }
    public Guid? FarmBaseMapVersionId { get; private set; }
    public string? ResultNature { get; private set; }
    public string? TeamLabelLimitation { get; private set; }
    public DateTimeOffset? ObservedAt { get; private set; }
    public long ReviewRevision { get; private set; }
    public DateTimeOffset? ReviewUpdatedAt { get; private set; }
    public Guid? ApprovedBatchId { get; private set; }
    public long? ApprovedReviewRevision { get; private set; }
    public JsonDocument? ApprovalSnapshot { get; private set; }
    public string? ApprovalFingerprint { get; private set; }

    public void ApproveReviewedPlantHealth(PlantHealthApprovalSnapshot snapshot, uint expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureVersion(expectedVersion);
        DomainGuard.NotEmpty(snapshot.OperationId); DomainGuard.NotEmpty(snapshot.ApprovedBy);
        DomainGuard.Utc(snapshot.ApprovedAt); ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.Reason);
        if (snapshot.Reason.Length > 2000 || ServiceType != SurveyServiceType.PlantHealth ||
            ResultNature != PlantHealthV4Values.ResultNature || snapshot.ResultNature != ResultNature ||
            snapshot.ResultId != Id || snapshot.SurveyOrderId != SurveyOrderId || snapshot.TenantId != TenantId || snapshot.FarmId != FarmId ||
            snapshot.BatchId != ActiveImportBatchId || snapshot.ReviewRevision != ReviewRevision ||
            snapshot.MissionId != SourceMissionId || snapshot.FarmBoundaryVersionId != FarmBoundaryVersionId ||
            snapshot.FarmBaseMapVersionId != FarmBaseMapVersionId || snapshot.TeamLabelLimitation != TeamLabelLimitation ||
            string.IsNullOrWhiteSpace(TeamLabelLimitation) || snapshot.SourceVersion <= 0 || snapshot.SourceFingerprint.Length != 64 ||
            snapshot.Reviews.Select(r => r.ReviewId).Distinct().Count() != snapshot.Reviews.Count ||
            snapshot.Reviews.Any(r => r.ReviewId == Guid.Empty || r.SubjectId == Guid.Empty || r.SnapshotFingerprint.Length != 64) ||
            snapshot.EvidenceMediaIds.Count == 0 || snapshot.EvidenceMediaIds.Contains(Guid.Empty) ||
            snapshot.AcceptedFindingCount < 0 || snapshot.Conclusion != (snapshot.AcceptedFindingCount == 0
                ? "NO_ACCEPTED_SUSPECTED_FINDINGS" : "HAS_REVIEWED_SUSPECTED_FINDINGS"))
            throw new InvalidOperationException("Approval must bind the active preliminary result and its exact reviewed candidate.");
        TransitionTo(SurveyResultStatus.Approved, snapshot.ApprovedAt);
        // Result provenance describes the active approved revision; older source snapshots remain on their batches.
        Provenance = JsonSerializer.SerializeToDocument(snapshot.Provenance);
        ReviewedBy = snapshot.ApprovedBy; ReviewedAt = snapshot.ApprovedAt;
        ApprovedBatchId = snapshot.BatchId; ApprovedReviewRevision = snapshot.ReviewRevision;
        ApprovalSnapshot = JsonSerializer.SerializeToDocument(snapshot);
        ApprovalFingerprint = PlantHealthV4Fingerprint.ForValue(snapshot);
    }

    public void RecordHumanReview(Guid batchId, DateTimeOffset changedAt)
    {
        if (Status != SurveyResultStatus.PendingReview || ActiveImportBatchId != batchId)
            throw new InvalidOperationException("Only the active pending batch can be reviewed.");
        DomainGuard.Utc(changedAt);
        SurveyTransitionGuard.EnsureTimestamp(changedAt, UpdatedAt);
        ReviewRevision = checked(ReviewRevision + 1); ReviewUpdatedAt = changedAt; UpdatedAt = changedAt;
    }

    public void ActivateImport(PlantHealthImportBatch batch, DateTimeOffset? observedAt, string limitation, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (Status != SurveyResultStatus.PendingReview || batch.SurveyResultId != Id ||
            batch.TenantId != TenantId || batch.FarmId != FarmId || batch.SurveyOrderId != SurveyOrderId)
            throw new InvalidOperationException("Only a matching pending import can become active.");
        DomainGuard.Utc(changedAt);
        ActiveImportBatchId = batch.Id; SourceMissionId = batch.MissionId;
        FarmBoundaryVersionId = batch.FarmBoundaryVersionId; FarmBaseMapVersionId = batch.FarmBaseMapVersionId;
        ResultNature = "PRELIMINARY_SUSPECTED"; TeamLabelLimitation = limitation;
        ObservedAt = observedAt; UpdatedAt = changedAt; Version++;
    }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;
    public ICollection<HarvestReadinessAssessment> HarvestReadinessAssessments { get; private set; } = [];

    public static bool CanTransition(SurveyResultStatus from, SurveyResultStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static SurveyResult CreateForManagerReview(
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        SurveyServiceType serviceType,
        JsonDocument provenance,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.Utc(createdAt);
        ArgumentNullException.ThrowIfNull(provenance);

        return new SurveyResult
        {
            Id = Guid.NewGuid(),
            SurveyOrderId = surveyOrderId,
            TenantId = tenantId,
            FarmId = farmId,
            ServiceType = serviceType,
            Status = SurveyResultStatus.PendingReview,
            Provenance = JsonDocument.Parse(provenance.RootElement.GetRawText()),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                SurveyResultDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public void Approve(Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        if (ResultNature == PlantHealthV4Values.ResultNature)
            throw new InvalidOperationException("V4 Plant Health requires approval of an exact reviewed candidate.");
        DomainGuard.NotEmpty(reviewedBy);
        TransitionTo(SurveyResultStatus.Approved, reviewedAt);
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    public void Publish(Guid publishedBy, DateTimeOffset publishedAt)
    {
        if (ResultNature == PlantHealthV4Values.ResultNature)
            throw new InvalidOperationException("V4 Plant Health requires publication of its frozen approval candidate.");
        PublishCore(publishedBy, publishedAt);
    }

    public void PublishReviewedPlantHealth(PlantHealthPublication publication, uint expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(publication);
        EnsureVersion(expectedVersion);
        if (ServiceType != SurveyServiceType.PlantHealth || ResultNature != PlantHealthV4Values.ResultNature ||
            ApprovalSnapshot is null || ApprovedReviewRevision != ReviewRevision ||
            publication.SurveyResultId != Id || publication.BatchId != ApprovedBatchId ||
            publication.BatchId != ActiveImportBatchId || publication.TenantId != TenantId || publication.FarmId != FarmId ||
            publication.ApprovalFingerprint != ApprovalFingerprint)
            throw new InvalidOperationException("Publication must retain the exact frozen approval and active batch.");
        PublishCore(publication.PublishedBy, publication.PublishedAt);
    }

    private void PublishCore(Guid publishedBy, DateTimeOffset publishedAt)
    {
        DomainGuard.NotEmpty(publishedBy);
        TransitionTo(SurveyResultStatus.Published, publishedAt);
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
    }

    private void TransitionTo(SurveyResultStatus target, DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            SurveyResultDomainErrorCodes.InvalidTransition);
        Status = target;
        UpdatedAt = occurredAt;
        Version++;
    }
}
