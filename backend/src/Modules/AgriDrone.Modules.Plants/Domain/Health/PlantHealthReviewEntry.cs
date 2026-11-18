using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Health;

public enum HealthReviewKind { Finding, Boundary, Zone, Guidance }
public enum HealthReviewDecision { Approve, Correct, Reject, NonDisease, Ambiguous, AcceptCorrectedPosition, ExcludeEvidence, NeedsEvidence, Select, RejectAll }

public sealed record ReviewedHealthMember(Guid FindingId, int SourceVersion, Guid ReviewId, Guid PlantId);
public sealed record ReviewedGuidanceSnapshot(Guid Id, string Code, int VersionNumber, string SourceReference,
    string SuspectedClassCode, Guid? PlantConditionId, int? PlantConditionRevisionNumber,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo);
public sealed record HealthReviewSnapshot(
    Guid? PlantId = null, string? ClassCode = null, Guid? ConditionId = null, int? ConditionRevision = null,
    double[]? Position = null, double[][][]? Geometry = null,
    IReadOnlyList<ReviewedHealthMember>? Members = null, Guid? BoundReviewId = null,
    Guid? BoundFindingId = null, ReviewedGuidanceSnapshot? Guidance = null,
    IReadOnlyList<Guid>? EvidenceMediaIds = null, string? AnnotationRuleVersion = null);

/// <summary>Append-only human decision. Kind identifies a strongly validated subject and snapshot.</summary>
public sealed class PlantHealthReviewEntry : Entity
{
    private PlantHealthReviewEntry() { }
    public Guid BatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public HealthReviewKind Kind { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid? FindingId { get; private set; }
    public Guid? ZoneId { get; private set; }
    public Guid? BoundaryExceptionId { get; private set; }
    public Guid? PreviousReviewId { get; private set; }
    public HealthReviewDecision Decision { get; private set; }
    public Guid ReviewedBy { get; private set; }
    public DateTimeOffset ReviewedAt { get; private set; }
    public string Reason { get; private set; } = null!;
    public JsonDocument Snapshot { get; private set; } = null!;

    public HealthReviewSnapshot ReadSnapshot() => Snapshot.Deserialize<HealthReviewSnapshot>()
        ?? throw new InvalidOperationException("Review snapshot is missing.");

    public static PlantHealthReviewEntry Create(Guid batchId, Guid tenantId, Guid farmId, Guid resultId,
        HealthReviewKind kind, Guid subjectId, Guid? previousId, HealthReviewDecision decision,
        Guid actorId, DateTimeOffset reviewedAt, string reason, HealthReviewSnapshot snapshot)
    {
        DomainGuard.NotEmpty(batchId); DomainGuard.NotEmpty(tenantId); DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(resultId); DomainGuard.NotEmpty(subjectId); DomainGuard.NotEmpty(actorId);
        DomainGuard.Utc(reviewedAt); ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, 2000);
        ArgumentNullException.ThrowIfNull(snapshot);
        var allowed = kind switch
        {
            HealthReviewKind.Finding => decision is HealthReviewDecision.Approve or HealthReviewDecision.Correct or HealthReviewDecision.Reject or HealthReviewDecision.NonDisease or HealthReviewDecision.Ambiguous,
            HealthReviewKind.Boundary => decision is HealthReviewDecision.AcceptCorrectedPosition or HealthReviewDecision.ExcludeEvidence or HealthReviewDecision.NeedsEvidence,
            HealthReviewKind.Zone => decision is HealthReviewDecision.Approve or HealthReviewDecision.Correct or HealthReviewDecision.Reject or HealthReviewDecision.Ambiguous,
            HealthReviewKind.Guidance => decision is HealthReviewDecision.Select or HealthReviewDecision.RejectAll or HealthReviewDecision.NeedsEvidence,
            _ => false
        };
        if (!allowed) throw new ArgumentException("Decision does not apply to this review kind.", nameof(decision));
        if (kind == HealthReviewKind.Finding && decision is HealthReviewDecision.Approve or HealthReviewDecision.Correct &&
            (snapshot.PlantId is null || snapshot.ClassCode is null || snapshot.ConditionId is null || snapshot.Position is not { Length: 2 }))
            throw new ArgumentException("Accepted finding requires a reviewed Plant, position and configured class.", nameof(snapshot));
        return new()
        {
            Id = Guid.CreateVersion7(), BatchId = batchId, TenantId = tenantId, FarmId = farmId, SurveyResultId = resultId,
            Kind = kind, SubjectId = subjectId, FindingId = kind == HealthReviewKind.Finding ? subjectId : null,
            ZoneId = kind is HealthReviewKind.Zone or HealthReviewKind.Guidance ? subjectId : null,
            BoundaryExceptionId = kind == HealthReviewKind.Boundary ? subjectId : null,
            PreviousReviewId = previousId, Decision = decision, ReviewedBy = actorId, ReviewedAt = reviewedAt,
            Reason = reason.Trim(), Snapshot = JsonSerializer.SerializeToDocument(snapshot)
        };
    }
}

public sealed class PlantHealthReviewState
{
    private PlantHealthReviewState() { }
    public Guid BatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public HealthReviewKind Kind { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid CurrentReviewId { get; private set; }
    public uint Version { get; private set; }
    public static PlantHealthReviewState Create(PlantHealthReviewEntry entry) => new()
    {
        BatchId = entry.BatchId, TenantId = entry.TenantId, FarmId = entry.FarmId, SurveyResultId = entry.SurveyResultId,
        Kind = entry.Kind, SubjectId = entry.SubjectId, CurrentReviewId = entry.Id
    };
    public void Advance(PlantHealthReviewEntry entry)
    {
        if (entry.BatchId != BatchId || entry.SubjectId != SubjectId || entry.Kind != Kind ||
            entry.TenantId != TenantId || entry.FarmId != FarmId || entry.SurveyResultId != SurveyResultId || entry.PreviousReviewId != CurrentReviewId)
            throw new InvalidOperationException("Review head context/previous revision does not match.");
        CurrentReviewId = entry.Id;
    }
}
