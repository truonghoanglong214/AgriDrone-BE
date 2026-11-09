using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;

namespace AgriDrone.Modules.Plants.Domain.Health;

/// <summary>Derived suspected state only. Multiple accepted findings for one Plant are retained.</summary>
public sealed class LatestReviewedPlantHealth
{
    private LatestReviewedPlantHealth() { }
    public Guid PlantId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid PublicationId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }
    public long PublicationVersion { get; private set; }
    public string ResultNature { get; private set; } = null!;
    public string TeamLabelLimitation { get; private set; } = null!;
    public JsonDocument Findings { get; private set; } = null!;
    public uint Version { get; private set; }

    public static LatestReviewedPlantHealth Create(Guid tenantId, PlantHealthPreliminaryResultPublishedV1 publication,
        IReadOnlyList<PublishedPlantHealthFindingV1> findings)
    {
        var latest = new LatestReviewedPlantHealth();
        latest.Apply(tenantId, publication, findings);
        return latest;
    }

    public bool TryAdvance(Guid tenantId, PlantHealthPreliminaryResultPublishedV1 publication,
        IReadOnlyList<PublishedPlantHealthFindingV1> findings)
    {
        Validate(tenantId, publication, findings);
        if (tenantId != TenantId || publication.FarmId != FarmId || findings[0].PlantId != PlantId)
            throw new InvalidOperationException("Latest-reviewed Plant context cannot change.");
        var observed = findings.Max(f => f.ObservedAt);
        var order = observed.CompareTo(ObservedAt);
        if (order < 0 || order == 0 && (publication.PublicationVersion < PublicationVersion ||
            publication.PublicationVersion == PublicationVersion && publication.SurveyResultId.CompareTo(SurveyResultId) <= 0)) return false;
        Apply(tenantId, publication, findings);
        return true;
    }

    private void Apply(Guid tenantId, PlantHealthPreliminaryResultPublishedV1 publication,
        IReadOnlyList<PublishedPlantHealthFindingV1> findings)
    {
        Validate(tenantId, publication, findings);
        PlantId = findings[0].PlantId; TenantId = tenantId; FarmId = publication.FarmId;
        PublicationId = publication.PublicationId; SurveyResultId = publication.SurveyResultId;
        ObservedAt = findings.Max(f => f.ObservedAt); PublicationVersion = publication.PublicationVersion;
        ResultNature = publication.ResultNature; TeamLabelLimitation = publication.TeamLabelLimitation;
        Findings = JsonSerializer.SerializeToDocument(findings.OrderBy(f => f.FindingId).ToArray());
    }

    private static void Validate(Guid tenantId, PlantHealthPreliminaryResultPublishedV1 publication,
        IReadOnlyList<PublishedPlantHealthFindingV1> findings)
    {
        if (tenantId == Guid.Empty || publication.PublicationId == Guid.Empty || publication.PublicationVersion <= 0 ||
            publication.ResultNature != PlantHealthV4Values.ResultNature || string.IsNullOrWhiteSpace(publication.TeamLabelLimitation) ||
            findings.Count == 0 || findings[0].PlantId == Guid.Empty || findings.Any(f => f.PlantId != findings[0].PlantId ||
                !publication.Findings.Contains(f) || f.ObservedAt == default || f.ObservedAt.Offset != TimeSpan.Zero))
            throw new InvalidOperationException("Latest-reviewed state requires accepted published findings for exactly one Plant.");
    }
}
