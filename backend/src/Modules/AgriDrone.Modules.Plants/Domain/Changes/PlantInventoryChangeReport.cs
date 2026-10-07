using System.Globalization;
using System.Text.Json;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Changes;

public sealed class PlantInventoryChangeReport : AggregateRoot
{
    private PlantInventoryChangeReport() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ReportedByUserId { get; private set; }
    public PlantInventoryChangeKind Kind { get; private set; }
    public PlantInventoryChangeStatus Status { get; private set; }
    public Guid? ExistingPlantId { get; private set; }
    public Point ReportedLocation { get; private set; } = null!;
    public string PoleLocationKey { get; private set; } = null!;
    public string CallerScope { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string ReportReason { get; private set; } = null!;
    public JsonDocument ReportEvidence { get; private set; } = null!;
    public Guid? ReviewStartedBy { get; private set; }
    public DateTimeOffset? ReviewStartedAt { get; private set; }
    public Guid? EvidenceRequestedBy { get; private set; }
    public DateTimeOffset? EvidenceRequestedAt { get; private set; }
    public string? EvidenceRequestReason { get; private set; }
    public JsonDocument? EvidenceRequestDetails { get; private set; }
    public Guid? EvidenceSurveyOrderId { get; private set; }
    public Guid? EvidenceMissionId { get; private set; }
    public string? EvidenceCandidateReference { get; private set; }
    public Guid? EvidenceFarmBoundaryVersionId { get; private set; }
    public Guid? EvidenceFarmBaseMapVersionId { get; private set; }
    public JsonDocument? SurveyEvidence { get; private set; }
    public Guid? VerifiedBy { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public string? VerificationReason { get; private set; }
    public JsonDocument? VerificationEvidence { get; private set; }
    public Guid? RejectedBy { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public JsonDocument? RejectionEvidence { get; private set; }
    public Guid? WithdrawnBy { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }
    public Guid? ResultingPlantId { get; private set; }
    public Guid? AppliedFarmBoundaryVersionId { get; private set; }
    public Guid? AppliedFarmBaseMapVersionId { get; private set; }
    public Guid? AppliedBy { get; private set; }
    public DateTimeOffset? AppliedAt { get; private set; }
    public string? ApplicationReason { get; private set; }
    public JsonDocument? ApplicationEvidence { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PlantInventoryChangeReport Create(
        Guid tenantId,
        Guid farmId,
        Guid reportedByUserId,
        PlantInventoryChangeKind kind,
        Guid? existingPlantId,
        Point reportedLocation,
        string callerScope,
        string idempotencyKey,
        string reason,
        JsonDocument evidence,
        DateTimeOffset submittedAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(reportedByUserId);
        DomainGuard.Utc(submittedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(evidence);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        EnsurePlantContext(kind, existingPlantId);
        var normalizedLocation = ValidPoint(reportedLocation, nameof(reportedLocation));
        var normalizedScope = Normalize(callerScope, 100, nameof(callerScope), lowerCase: true);
        var normalizedKey = Normalize(idempotencyKey, 200, nameof(idempotencyKey));
        var normalizedReason = Normalize(reason, 2000, nameof(reason));

        return new PlantInventoryChangeReport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            ReportedByUserId = reportedByUserId,
            Kind = kind,
            Status = PlantInventoryChangeStatus.Submitted,
            ExistingPlantId = existingPlantId,
            ReportedLocation = normalizedLocation,
            PoleLocationKey = CreatePoleLocationKey(normalizedLocation),
            CallerScope = normalizedScope,
            IdempotencyKey = normalizedKey,
            ReportReason = normalizedReason,
            ReportEvidence = CloneJson(evidence),
            CreatedAt = submittedAt,
            UpdatedAt = submittedAt
        };
    }

    public void StartReview(Guid managerId, DateTimeOffset startedAt)
    {
        EnsureStatus(PlantInventoryChangeStatus.Submitted);
        ValidateActorAndTimestamp(managerId, startedAt);

        ReviewStartedBy = managerId;
        ReviewStartedAt = startedAt;
        SetStatus(PlantInventoryChangeStatus.UnderReview, startedAt);
    }

    public void Verify(
        Guid managerId,
        DateTimeOffset verifiedAt,
        string reason,
        JsonDocument evidence)
    {
        EnsureStatus(PlantInventoryChangeStatus.UnderReview);
        if (Kind == PlantInventoryChangeKind.NewPlant)
        {
            throw DomainError(
                PlantInventoryChangeDomainErrorCodes.SurveyEvidenceRequired,
                "A new-plant report requires later-survey evidence before verification.");
        }

        RecordVerification(managerId, verifiedAt, reason, evidence);
        SetStatus(PlantInventoryChangeStatus.Verified, verifiedAt);
    }

    public void RequestSurveyEvidence(
        Guid managerId,
        DateTimeOffset requestedAt,
        string reason,
        JsonDocument details)
    {
        EnsureStatus(PlantInventoryChangeStatus.UnderReview);
        if (Kind != PlantInventoryChangeKind.NewPlant)
        {
            throw DomainError(
                PlantInventoryChangeDomainErrorCodes.SurveyEvidenceNotAllowed,
                "Only a new-plant report may wait for later-survey evidence.");
        }

        ValidateDecision(managerId, requestedAt, reason, details);
        EvidenceRequestedBy = managerId;
        EvidenceRequestedAt = requestedAt;
        EvidenceRequestReason = Normalize(reason, 2000, nameof(reason));
        EvidenceRequestDetails = CloneJson(details);
        SetStatus(PlantInventoryChangeStatus.AwaitingSurveyEvidence, requestedAt);
    }

    public void VerifyWithSurveyEvidence(
        Guid surveyOrderId,
        Guid missionId,
        string candidateReference,
        Guid farmBoundaryVersionId,
        Guid? farmBaseMapVersionId,
        JsonDocument surveyEvidence,
        Guid managerId,
        DateTimeOffset verifiedAt,
        string reason,
        JsonDocument verificationEvidence)
    {
        EnsureStatus(PlantInventoryChangeStatus.AwaitingSurveyEvidence);
        if (Kind != PlantInventoryChangeKind.NewPlant)
        {
            throw DomainError(
                PlantInventoryChangeDomainErrorCodes.SurveyEvidenceNotAllowed,
                "Later-survey evidence may only verify a new-plant report.");
        }

        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(missionId);
        DomainGuard.NotEmpty(farmBoundaryVersionId);
        if (farmBaseMapVersionId.HasValue)
        {
            DomainGuard.NotEmpty(farmBaseMapVersionId.Value, nameof(farmBaseMapVersionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(candidateReference);
        ArgumentNullException.ThrowIfNull(surveyEvidence);
        var normalizedCandidateReference = Normalize(
            candidateReference,
            200,
            nameof(candidateReference));

        RecordVerification(managerId, verifiedAt, reason, verificationEvidence);
        EvidenceSurveyOrderId = surveyOrderId;
        EvidenceMissionId = missionId;
        EvidenceCandidateReference = normalizedCandidateReference;
        EvidenceFarmBoundaryVersionId = farmBoundaryVersionId;
        EvidenceFarmBaseMapVersionId = farmBaseMapVersionId;
        SurveyEvidence = CloneJson(surveyEvidence);
        SetStatus(PlantInventoryChangeStatus.Verified, verifiedAt);
    }

    public void Reject(
        Guid managerId,
        DateTimeOffset rejectedAt,
        string reason,
        JsonDocument evidence)
    {
        if (Status is not PlantInventoryChangeStatus.UnderReview and
            not PlantInventoryChangeStatus.AwaitingSurveyEvidence)
        {
            throw InvalidTransition(PlantInventoryChangeStatus.Rejected);
        }

        ValidateDecision(managerId, rejectedAt, reason, evidence);
        RejectedBy = managerId;
        RejectedAt = rejectedAt;
        RejectionReason = Normalize(reason, 2000, nameof(reason));
        RejectionEvidence = CloneJson(evidence);
        SetStatus(PlantInventoryChangeStatus.Rejected, rejectedAt);
    }

    public void Withdraw(Guid ownerUserId, DateTimeOffset withdrawnAt)
    {
        if (Status is not PlantInventoryChangeStatus.Submitted and
            not PlantInventoryChangeStatus.UnderReview and
            not PlantInventoryChangeStatus.AwaitingSurveyEvidence)
        {
            throw InvalidTransition(PlantInventoryChangeStatus.Withdrawn);
        }

        DomainGuard.NotEmpty(ownerUserId);
        if (ownerUserId != ReportedByUserId)
        {
            throw DomainError(
                PlantInventoryChangeDomainErrorCodes.ReporterMismatch,
                "Only the owner who submitted the report may withdraw it.");
        }

        EnsureTimestamp(withdrawnAt);
        WithdrawnBy = ownerUserId;
        WithdrawnAt = withdrawnAt;
        SetStatus(PlantInventoryChangeStatus.Withdrawn, withdrawnAt);
    }

    public void MarkApplied(
        Guid? resultingPlantId,
        Guid farmBoundaryVersionId,
        Guid farmBaseMapVersionId,
        Guid appliedBy,
        DateTimeOffset appliedAt,
        string reason,
        JsonDocument evidence)
    {
        EnsureStatus(PlantInventoryChangeStatus.Verified);
        EnsureResultingPlant(resultingPlantId);
        DomainGuard.NotEmpty(farmBoundaryVersionId);
        DomainGuard.NotEmpty(farmBaseMapVersionId);
        ValidateDecision(appliedBy, appliedAt, reason, evidence);

        ResultingPlantId = resultingPlantId;
        AppliedFarmBoundaryVersionId = farmBoundaryVersionId;
        AppliedFarmBaseMapVersionId = farmBaseMapVersionId;
        AppliedBy = appliedBy;
        AppliedAt = appliedAt;
        ApplicationReason = Normalize(reason, 2000, nameof(reason));
        ApplicationEvidence = CloneJson(evidence);
        SetStatus(PlantInventoryChangeStatus.Applied, appliedAt);
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw DomainError(
                PlantInventoryChangeDomainErrorCodes.VersionConflict,
                $"Expected PlantInventoryChangeReport version {expectedVersion}, but found {Version}.");
        }
    }

    private void RecordVerification(
        Guid managerId,
        DateTimeOffset verifiedAt,
        string reason,
        JsonDocument evidence)
    {
        ValidateDecision(managerId, verifiedAt, reason, evidence);
        VerifiedBy = managerId;
        VerifiedAt = verifiedAt;
        VerificationReason = Normalize(reason, 2000, nameof(reason));
        VerificationEvidence = CloneJson(evidence);
    }

    private void ValidateDecision(
        Guid actorId,
        DateTimeOffset occurredAt,
        string reason,
        JsonDocument evidence)
    {
        ValidateActorAndTimestamp(actorId, occurredAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(evidence);
    }

    private void ValidateActorAndTimestamp(Guid actorId, DateTimeOffset occurredAt)
    {
        DomainGuard.NotEmpty(actorId);
        EnsureTimestamp(occurredAt);
    }

    private void EnsureTimestamp(DateTimeOffset occurredAt)
    {
        DomainGuard.Utc(occurredAt);
        if (occurredAt < UpdatedAt)
        {
            throw new ArgumentException(
                "Lifecycle time cannot be earlier than the last update.",
                nameof(occurredAt));
        }
    }

    private void EnsureStatus(PlantInventoryChangeStatus expected)
    {
        if (Status != expected)
        {
            throw InvalidTransition(expected);
        }
    }

    private PlantInventoryChangeDomainException InvalidTransition(
        PlantInventoryChangeStatus target) =>
        DomainError(
            PlantInventoryChangeDomainErrorCodes.InvalidTransition,
            $"Plant inventory change cannot transition from {Status} to {target}.");

    private void SetStatus(
        PlantInventoryChangeStatus status,
        DateTimeOffset occurredAt)
    {
        Status = status;
        UpdatedAt = occurredAt;
        Version++;
    }

    private void EnsureResultingPlant(Guid? resultingPlantId)
    {
        if (Kind == PlantInventoryChangeKind.Removed)
        {
            if (resultingPlantId.HasValue)
            {
                throw DomainError(
                    PlantInventoryChangeDomainErrorCodes.ResultingPlantNotAllowed,
                    "A removal report cannot produce a resulting Plant.");
            }

            return;
        }

        if (!resultingPlantId.HasValue || resultingPlantId.Value == Guid.Empty)
        {
            throw DomainError(
                PlantInventoryChangeDomainErrorCodes.ResultingPlantRequired,
                "A replacement or new-plant report must produce a resulting Plant.");
        }

        if (Kind == PlantInventoryChangeKind.Replaced &&
            resultingPlantId == ExistingPlantId)
        {
            throw DomainError(
                PlantInventoryChangeDomainErrorCodes.ReplacementPlantMustDiffer,
                "A replacement must use a new Plant identity.");
        }
    }

    private static void EnsurePlantContext(
        PlantInventoryChangeKind kind,
        Guid? existingPlantId)
    {
        if (kind is PlantInventoryChangeKind.Removed or PlantInventoryChangeKind.Replaced)
        {
            if (!existingPlantId.HasValue || existingPlantId.Value == Guid.Empty)
            {
                throw DomainError(
                    PlantInventoryChangeDomainErrorCodes.ExistingPlantRequired,
                    "Removed and replaced reports require an existing Plant.");
            }

            return;
        }

        if (existingPlantId.HasValue)
        {
            throw DomainError(
                PlantInventoryChangeDomainErrorCodes.ExistingPlantNotAllowed,
                "A new-plant report cannot reference an existing Plant.");
        }
    }

    private static Point ValidPoint(Point point, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(point, parameterName);
        if (point.IsEmpty || point.SRID != 4326 ||
            !double.IsFinite(point.X) || !double.IsFinite(point.Y) ||
            point.X is < -180 or > 180 || point.Y is < -90 or > 90)
        {
            throw new ArgumentException(
                "Plant location must be a finite WGS84 Point.",
                parameterName);
        }

        return new Point(point.X, point.Y) { SRID = 4326 };
    }

    private static string CreatePoleLocationKey(Point point)
    {
        var longitude = Math.Round(point.X, 7, MidpointRounding.AwayFromZero);
        var latitude = Math.Round(point.Y, 7, MidpointRounding.AwayFromZero);
        return FormattableString.Invariant($"{longitude:F7}:{latitude:F7}");
    }

    private static string Normalize(
        string value,
        int maxLength,
        string parameterName,
        bool lowerCase = false)
    {
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maxLength.ToString(CultureInfo.InvariantCulture)} characters.",
                parameterName);
        }

        return lowerCase ? normalized.ToLowerInvariant() : normalized;
    }

    private static JsonDocument CloneJson(JsonDocument document) =>
        JsonDocument.Parse(document.RootElement.GetRawText());

    private static PlantInventoryChangeDomainException DomainError(
        string code,
        string message) => new(code, message);
}
