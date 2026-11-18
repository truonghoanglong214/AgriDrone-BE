using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

/// <summary>Immutable publication history, independent from mutable catalogue/read models.</summary>
public sealed class PlantHealthPublication : Entity
{
    private PlantHealthPublication() { }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public Guid BatchId { get; private set; }
    public long PublicationVersion { get; private set; }
    public Guid PublishedBy { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public string ApprovalFingerprint { get; private set; } = null!;
    public string Fingerprint { get; private set; } = null!;
    public JsonDocument Payload { get; private set; } = null!;

    public static PlantHealthPublication Create(Guid tenantId, PlantHealthPreliminaryResultPublishedV1 payload)
    {
        DomainGuard.NotEmpty(tenantId); DomainGuard.NotEmpty(payload.PublicationId);
        DomainGuard.NotEmpty(payload.FarmId); DomainGuard.NotEmpty(payload.SurveyResultId);
        DomainGuard.NotEmpty(payload.BatchId); DomainGuard.NotEmpty(payload.PublishedBy);
        DomainGuard.Utc(payload.PublishedAt);
        if (payload.PublicationVersion <= 0 || payload.ApprovedReviewRevision < 0 ||
            payload.ResultNature != PlantHealthV4Values.ResultNature || payload.Outcome != "PUBLISHED" ||
            string.IsNullOrWhiteSpace(payload.TeamLabelLimitation) || payload.ApprovalFingerprint.Length != 64 ||
            payload.Findings.Select(f => f.FindingId).Distinct().Count() != payload.Findings.Count ||
            payload.DiseaseZones.Select(z => z.ZoneId).Distinct().Count() != payload.DiseaseZones.Count)
            throw new InvalidOperationException("Publication requires a unique, reviewed preliminary candidate.");
        return new()
        {
            Id = payload.PublicationId, TenantId = tenantId, FarmId = payload.FarmId,
            SurveyResultId = payload.SurveyResultId, BatchId = payload.BatchId,
            PublicationVersion = payload.PublicationVersion, PublishedBy = payload.PublishedBy,
            PublishedAt = payload.PublishedAt, ApprovalFingerprint = payload.ApprovalFingerprint,
            Fingerprint = PlantHealthV4Fingerprint.ForValue(payload), Payload = JsonSerializer.SerializeToDocument(payload)
        };
    }
}
