using System.Text.Json;
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
    public Guid HealthLevelId { get; private set; }
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
        EnsureStatus(DiseaseZoneStatus.Proposed);
        ValidateDecisionActor(reviewedBy, reviewedAt, reason, evidence);

        if ((selectedRecommendation is null) == !rejectAllRecommendations)
        {
            throw new DiseaseZoneDomainException(
                DiseaseZoneDomainErrorCodes.RecommendationDecisionRequired,
                "Review must select one applicable recommendation or reject all candidates.");
        }

        if (selectedRecommendation is not null &&
            !selectedRecommendation.IsApplicable(PlantConditionId, HealthLevelId, reviewedAt))
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
