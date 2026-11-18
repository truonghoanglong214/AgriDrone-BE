using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Plants.Domain.Recommendations;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.DiseaseZones;

public sealed class DiseaseZone : AggregateRoot
{
    private readonly List<DiseaseZonePlantMembership> _memberships = [];

    private DiseaseZone() { }

    public Guid ZoneKey { get; private set; }
    public int VersionNumber { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public Guid FarmBaseMapVersionId { get; private set; }
    public Guid PlantConditionId { get; private set; }
    public Guid? HealthLevelId { get; private set; }
    public int SourceSchemaVersion { get; private set; } = 3;
    public Guid? ImportBatchId { get; private set; }
    public string? SuspectedClassCode { get; private set; }
    public Guid? SelectedSourcedGuidanceId { get; private set; }
    public JsonDocument? GuidanceSuggestions { get; private set; }
    public bool HasBlockingItems { get; private set; }
    public Guid? CurrentHealthReviewId { get; private set; }
    public string? ReviewedSuspectedClassCode { get; private set; }
    public Guid? ReviewedPlantConditionId { get; private set; }
    public string? GuidanceDecision { get; private set; }
    public Guid SourceHandoffId { get; private set; }
    public Guid? SourceJobId { get; private set; }
    public string SourceProposalId { get; private set; } = null!;
    public Polygon ProposedGeometry { get; private set; } = null!;
    public Polygon? ReviewedGeometry { get; private set; }
    public JsonDocument RecommendationCandidates { get; private set; } = null!;
    public JsonDocument ProposalEvidence { get; private set; } = null!;
    public int CurrentMembershipVersion { get; private set; }
    public DiseaseZoneStatus Status { get; private set; }
    public Guid? SelectedTreatmentRecommendationId { get; private set; }
    public bool RecommendationsRejected { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public JsonDocument? ReviewEvidence { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public Guid? SupersedesDiseaseZoneId { get; private set; }
    public Guid? SupersededByDiseaseZoneId { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<DiseaseZonePlantMembership> Memberships => _memberships;

    public void ProjectHealthReview(PlantHealthReviewEntry review, PlantHealthReviewEntry? guidance,
        bool valid, DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(review);
        if (SourceSchemaVersion != 4 || review.Kind != HealthReviewKind.Zone || review.SubjectId != Id ||
            review.BatchId != ImportBatchId || review.TenantId != TenantId || review.FarmId != FarmId ||
            Status is DiseaseZoneStatus.Published or DiseaseZoneStatus.Superseded)
            throw new InvalidOperationException("Only matching pending V4 zone reviews can be projected.");
        var snapshot = review.ReadSnapshot();
        var guidanceSnapshot = guidance?.ReadSnapshot();
        if (guidance is not null && (guidance.Kind != HealthReviewKind.Guidance || guidance.SubjectId != Id ||
            guidance.BatchId != ImportBatchId || guidanceSnapshot!.BoundReviewId != review.Id))
            throw new InvalidOperationException("Guidance review does not belong to this zone revision.");
        DomainGuard.Utc(changedAt);
        CurrentHealthReviewId = review.Id; ReviewedSuspectedClassCode = snapshot.ClassCode;
        ReviewedPlantConditionId = snapshot.ConditionId; GuidanceDecision = guidance?.Decision.ToString();
        SelectedSourcedGuidanceId = guidanceSnapshot?.Guidance?.Id;
        ReviewedGeometry = null; HasBlockingItems = !valid;
        ReviewedBy = review.ReviewedBy; ReviewedAt = review.ReviewedAt; ReviewReason = review.Reason;
        ReviewEvidence = JsonDocument.Parse(review.Snapshot.RootElement.GetRawText());
        Status = review.Decision == HealthReviewDecision.Reject ? DiseaseZoneStatus.Rejected : DiseaseZoneStatus.Proposed;
        if (valid && review.Decision is HealthReviewDecision.Approve or HealthReviewDecision.Correct)
        {
            if (snapshot.Geometry is null || snapshot.Members is not { Count: > 0 } ||
                guidance?.Decision is not (HealthReviewDecision.Select or HealthReviewDecision.RejectAll))
                throw new InvalidOperationException("Reviewed V4 zones require geometry, memberships and a final guidance decision.");
            var rings = snapshot.Geometry.Select(r => new LinearRing(r.Select(p => new Coordinate(p[0], p[1])).ToArray()) { SRID = 4326 }).ToArray();
            ReviewedGeometry = DiseaseZoneGeometryGuard.ValidPolygon(new Polygon(rings[0], rings.Skip(1).ToArray()) { SRID = 4326 }, nameof(review));
            CurrentMembershipVersion = checked(Math.Max(CurrentMembershipVersion, _memberships.Select(m => m.MembershipVersion).DefaultIfEmpty(1).Max()) + 1);
            AddMembershipSnapshot(snapshot.Members.Select(m => new DiseaseZoneMembershipCandidate(m.PlantId, null)).ToArray(),
                CurrentMembershipVersion, DiseaseZoneMembershipKind.Reviewed, changedAt);
            Status = DiseaseZoneStatus.Reviewed;
        }
        UpdatedAt = changedAt;
    }

    public static DiseaseZone CreatePendingHealthProposal(Guid tenantId, Guid farmId, Guid orderId,
        Guid resultId, Guid batchId, Guid boundaryId, Guid baseMapId, Guid handoffId, Guid jobId,
        Guid conditionId, ProposedDiseaseZoneV4 source, Polygon geometry, bool hasBlockingItems, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        DomainGuard.Utc(createdAt);
        var zone = new DiseaseZone
        {
            Id = Guid.CreateVersion7(), ZoneKey = Guid.CreateVersion7(), VersionNumber = 1,
            TenantId = tenantId, FarmId = farmId, SurveyOrderId = orderId, SurveyResultId = resultId,
            FarmBoundaryVersionId = boundaryId, FarmBaseMapVersionId = baseMapId,
            PlantConditionId = conditionId, SourceSchemaVersion = 4, ImportBatchId = batchId,
            SuspectedClassCode = source.SuspectedClassCode, SourceHandoffId = handoffId, SourceJobId = jobId,
            SourceProposalId = source.SourceProposalId, ProposedGeometry = DiseaseZoneGeometryGuard.ValidPolygon(geometry, nameof(geometry)),
            RecommendationCandidates = JsonDocument.Parse("[]"),
            GuidanceSuggestions = JsonSerializer.SerializeToDocument(source.GuidanceCandidates),
            ProposalEvidence = JsonSerializer.SerializeToDocument(source), HasBlockingItems = hasBlockingItems,
            CurrentMembershipVersion = source.MembershipVersion, Status = DiseaseZoneStatus.Proposed,
            CreatedAt = createdAt, UpdatedAt = createdAt
        };
        foreach (var member in source.Memberships)
            zone._memberships.Add(DiseaseZonePlantMembership.CreatePending(zone.Id, farmId, member, source.MembershipVersion, createdAt));
        return zone;
    }

    public static DiseaseZone CreateProposal(DiseaseZoneProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        proposal.Validate();

        var zone = new DiseaseZone
        {
            Id = Guid.NewGuid(),
            ZoneKey = Guid.NewGuid(),
            VersionNumber = 1,
            TenantId = proposal.TenantId,
            FarmId = proposal.FarmId,
            SurveyOrderId = proposal.SurveyOrderId,
            SurveyResultId = proposal.SurveyResultId,
            FarmBoundaryVersionId = proposal.FarmBoundaryVersionId,
            FarmBaseMapVersionId = proposal.FarmBaseMapVersionId,
            PlantConditionId = proposal.PlantConditionId,
            HealthLevelId = proposal.HealthLevelId,
            SourceHandoffId = proposal.SourceHandoffId,
            SourceJobId = proposal.SourceJobId,
            SourceProposalId = proposal.SourceProposalId.Trim(),
            ProposedGeometry = DiseaseZoneGeometryGuard.ValidPolygon(
                proposal.Geometry,
                nameof(proposal.Geometry)),
            RecommendationCandidates = CloneJson(proposal.RecommendationCandidates),
            ProposalEvidence = CloneJson(proposal.Evidence),
            CurrentMembershipVersion = 1,
            Status = DiseaseZoneStatus.Proposed,
            CreatedAt = proposal.ProposedAt,
            UpdatedAt = proposal.ProposedAt
        };

        zone.AddMembershipSnapshot(
            proposal.Membership,
            1,
            DiseaseZoneMembershipKind.Proposed,
            proposal.ProposedAt);

        return zone;
    }

    public DiseaseZone CreateRevisionProposal(
        DiseaseZoneProposal proposal)
    {
        if (Status != DiseaseZoneStatus.Published)
        {
            throw InvalidTransition("Only a published DiseaseZone can be revised.");
        }

        ArgumentNullException.ThrowIfNull(proposal);
        proposal.Validate();

        if (proposal.TenantId != TenantId ||
            proposal.FarmId != FarmId ||
            proposal.PlantConditionId != PlantConditionId)
        {
            throw new DiseaseZoneDomainException(
                DiseaseZoneDomainErrorCodes.ReplacementMismatch,
                "A DiseaseZone revision must keep the same tenant, Farm and condition.");
        }

        var revision = CreateProposal(proposal);
        revision.ZoneKey = ZoneKey;
        revision.VersionNumber = checked(VersionNumber + 1);
        revision.SupersedesDiseaseZoneId = Id;
        return revision;
    }

    public void Review(
        Polygon reviewedGeometry,
        IReadOnlyCollection<DiseaseZoneMembershipCandidate> reviewedMembership,
        TreatmentRecommendation? selectedRecommendation,
        bool rejectAllRecommendations,
        Guid reviewedBy,
        DateTimeOffset reviewedAt,
        string reason,
        JsonDocument evidence)
    {
        if (SourceSchemaVersion == 4) throw new InvalidOperationException("V4 zones require sourced-guidance review, not legacy severity review.");
        EnsureStatus(DiseaseZoneStatus.Proposed);
        ValidateDecisionActor(reviewedBy, reviewedAt, reason, evidence);

        if ((selectedRecommendation is null) == !rejectAllRecommendations)
        {
            throw new DiseaseZoneDomainException(
                DiseaseZoneDomainErrorCodes.RecommendationDecisionRequired,
                "Review must select one applicable recommendation or reject all candidates.");
        }

        if (selectedRecommendation is not null &&
            !selectedRecommendation.IsApplicable(PlantConditionId, HealthLevelId!.Value, reviewedAt))
        {
            throw new DiseaseZoneDomainException(
                DiseaseZoneDomainErrorCodes.RecommendationNotApplicable,
                "Selected recommendation is not published and applicable to this condition and severity.");
        }

        ReviewedGeometry = DiseaseZoneGeometryGuard.ValidPolygon(
            reviewedGeometry,
            nameof(reviewedGeometry));
        CurrentMembershipVersion = checked(CurrentMembershipVersion + 1);
        AddMembershipSnapshot(
            reviewedMembership,
            CurrentMembershipVersion,
            DiseaseZoneMembershipKind.Reviewed,
            reviewedAt);
        SelectedTreatmentRecommendationId = selectedRecommendation?.Id;
        RecommendationsRejected = rejectAllRecommendations;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewReason = reason.Trim();
        ReviewEvidence = CloneJson(evidence);
        Status = DiseaseZoneStatus.Reviewed;
        UpdatedAt = reviewedAt;
        Version++;
    }

    public void Reject(
        Guid reviewedBy,
        DateTimeOffset reviewedAt,
        string reason,
        JsonDocument evidence)
    {
        if (SourceSchemaVersion == 4) throw new InvalidOperationException("V4 zones use the human review workflow.");
        EnsureStatus(DiseaseZoneStatus.Proposed);
        ValidateDecisionActor(reviewedBy, reviewedAt, reason, evidence);

        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewReason = reason.Trim();
        ReviewEvidence = CloneJson(evidence);
        Status = DiseaseZoneStatus.Rejected;
        UpdatedAt = reviewedAt;
        Version++;
    }

    public void Publish(Guid publishedBy, DateTimeOffset publishedAt)
    {
        EnsureStatus(DiseaseZoneStatus.Reviewed);
        DomainGuard.NotEmpty(publishedBy);
        EnsureTimestamp(publishedAt);

        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
        Status = DiseaseZoneStatus.Published;
        UpdatedAt = publishedAt;
        Version++;
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new DiseaseZoneDomainException(
                DiseaseZoneDomainErrorCodes.VersionConflict,
                $"Expected DiseaseZone version {expectedVersion}, but found {Version}.");
        }
    }

    internal void EnsureCanSupersede(DiseaseZone replacement, DateTimeOffset supersededAt)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        EnsureStatus(DiseaseZoneStatus.Published);
        EnsureTimestamp(supersededAt);

        if (replacement.Status != DiseaseZoneStatus.Reviewed ||
            replacement.ZoneKey != ZoneKey ||
            replacement.VersionNumber <= VersionNumber ||
            replacement.SupersedesDiseaseZoneId != Id ||
            replacement.TenantId != TenantId ||
            replacement.FarmId != FarmId)
        {
            throw new DiseaseZoneDomainException(
                DiseaseZoneDomainErrorCodes.ReplacementMismatch,
                "Replacement must be a newer reviewed DiseaseZone version for the same Farm.");
        }
    }

    internal void Supersede(Guid replacementId, DateTimeOffset supersededAt)
    {
        SupersededByDiseaseZoneId = replacementId;
        SupersededAt = supersededAt;
        Status = DiseaseZoneStatus.Superseded;
        UpdatedAt = supersededAt;
        Version++;
    }

    private void AddMembershipSnapshot(
        IReadOnlyCollection<DiseaseZoneMembershipCandidate> candidates,
        int membershipVersion,
        DiseaseZoneMembershipKind kind,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            throw new DiseaseZoneDomainException(
                DiseaseZoneDomainErrorCodes.EmptyMembership,
                "A DiseaseZone membership snapshot must contain at least one Plant.");
        }

        if (candidates.Select(candidate => candidate.PlantId).Distinct().Count() != candidates.Count)
        {
            throw new DiseaseZoneDomainException(
                DiseaseZoneDomainErrorCodes.DuplicateMembership,
                "A Plant cannot appear twice in the same DiseaseZone membership version.");
        }

        _memberships.AddRange(candidates.Select(candidate =>
            DiseaseZonePlantMembership.Create(
                Id,
                FarmId,
                candidate,
                membershipVersion,
                kind,
                createdAt)));
    }

    private void ValidateDecisionActor(
        Guid actorId,
        DateTimeOffset occurredAt,
        string reason,
        JsonDocument evidence)
    {
        DomainGuard.NotEmpty(actorId);
        EnsureTimestamp(occurredAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(evidence);

        if (reason.Trim().Length > 2000)
        {
            throw new ArgumentException("Review reason cannot exceed 2000 characters.", nameof(reason));
        }
    }

    private void EnsureTimestamp(DateTimeOffset occurredAt)
    {
        DomainGuard.Utc(occurredAt);
        if (occurredAt < UpdatedAt)
        {
            throw new ArgumentException("Lifecycle time cannot be earlier than the last update.", nameof(occurredAt));
        }
    }

    private void EnsureStatus(DiseaseZoneStatus expected)
    {
        if (Status != expected)
        {
            throw InvalidTransition($"DiseaseZone must be {expected} for this operation.");
        }
    }

    private static DiseaseZoneDomainException InvalidTransition(string message) =>
        new(DiseaseZoneDomainErrorCodes.InvalidTransition, message);

    private static JsonDocument CloneJson(JsonDocument document) =>
        JsonDocument.Parse(document.RootElement.GetRawText());
}
