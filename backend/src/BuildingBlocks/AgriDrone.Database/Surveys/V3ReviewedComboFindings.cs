using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class V3ReviewedComboFindings(
    ComboAnalysisReadyV1 source, ComboReviewDecisionsSnapshotRecord decisions)
{
    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = true };

    public ComboAnalysisReadyV1 Source { get; } = source;
    public ComboReviewDecisionsSnapshotRecord Decisions { get; } = decisions;
    public IReadOnlyDictionary<Guid, ComboFindingReviewDecisionRecord> AcceptedDecisions { get; } = decisions.FindingDecisions
        .Where(decision => decision.Decision == SurveyComboReviewRevision.FindingDecisionAccept)
        .ToDictionary(decision => decision.FindingId);

    public static async Task<V3ReviewedComboFindings?> ReadAsync(
        AgriDroneSchemaDbContext db, SurveyInternalApprovedResultRevision approval,
        SurveyComboImportBatch batch, CancellationToken cancellationToken)
    {
        var review = await db.Set<SurveyComboReviewRevision>().AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == approval.ComboReviewRevisionId && r.ComboBatchId == batch.Id &&
                r.SurveyOrderId == approval.SurveyOrderId && r.TenantId == approval.TenantId &&
                r.FarmId == approval.FarmId && r.Outcome == SurveyComboReviewRevision.OutcomeApproved &&
                r.DecisionFingerprint == approval.ComboReviewFingerprint, cancellationToken);
        if (review is null)
            return null;

        var decisions = review.DecisionsPayload.Deserialize<ComboReviewDecisionsSnapshotRecord>(PayloadOptions);
        var source = batch.RawPayload.Deserialize<ComboAnalysisReadyV1>(PayloadOptions);
        return decisions is not null && source is not null ? new(source, decisions) : null;
    }

    public IEnumerable<(ComboFindingV1 Finding, Guid PlantProfileId)> AcceptedFindings(IReadOnlySet<Guid> poleIds)
    {
        foreach (var finding in Source.Findings)
        {
            if (AcceptedDecisions.TryGetValue(finding.FindingId, out var decision) &&
                (decision.ReviewedPlantProfileId ?? finding.PlantProfileId) is Guid plantId &&
                poleIds.Contains(plantId))
            {
                yield return (finding, plantId);
            }
        }
    }

    public static Guid[] ReadEvidenceMediaIds(ComboFindingV1 finding, IReadOnlySet<Guid> releasedIds) =>
        finding.EvidenceMedia
            .Where(media => IsDerivedImage(media) || media.Role == "source_image")
            .Where(media => releasedIds.Contains(media.MediaAssetId))
            .OrderBy(media => media.Role switch { "crop_image" => 0, "annotated_image" => 1, _ => 2 })
            .Select(media => media.MediaAssetId)
            .Distinct()
            .ToArray();

    public static bool IsDerivedImage(EvidenceMediaReferenceV4 media) =>
        media.Role is "crop_image" or "annotated_image";
}
