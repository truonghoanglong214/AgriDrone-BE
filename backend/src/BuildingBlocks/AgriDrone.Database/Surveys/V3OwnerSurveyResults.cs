using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class V3OwnerSurveyResults(
    AgriDroneSchemaDbContext db, IV3ResultReleaseProofReader releases, IObjectStorage storage)
    : IV3OwnerSurveyResults
{
    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<V3OwnerSurveySummary>?> GetHistoryAsync(
        Guid tenantId, Guid farmId, CancellationToken cancellationToken)
    {
        if (!await FarmAvailableAsync(tenantId, farmId, cancellationToken))
            return null;

        var rows = await db.Database.SqlQuery<string>($"""
            SELECT r.proof::text AS "Value"
            FROM survey.v3_survey_releases r
            WHERE r.tenant_id = {tenantId} AND r.farm_id = {farmId}
              AND NOT EXISTS (SELECT 1 FROM survey.quality_refund_cases c
                  WHERE c.v3_publication_id = r.id AND c.status = 'Refunded')
            ORDER BY r.published_at DESC, r.id DESC
            """).ToListAsync(cancellationToken);
        return rows.Select(json => Summary(JsonSerializer.Deserialize<V3ResultReleaseProof>(json)!)).ToArray();
    }

    public async Task<V3OwnerSurveyResult?> GetAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var proof = await ReadVisibleProofAsync(tenantId, orderId, cancellationToken);
        if (proof is null)
            return null;

        var approval = await db.Set<SurveyInternalApprovedResultRevision>().AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == proof.ApprovedResultRevisionId &&
                a.SurveyOrderId == orderId && a.TenantId == tenantId && a.FarmId == proof.FarmId &&
                a.EvidenceFingerprint == proof.ApprovedResultFingerprint, cancellationToken);
        if (approval is null)
            return null;

        var poles = await ReadPolesAsync(proof, approval.PoleCountSnapshotId, cancellationToken);
        var boundary = await db.Database.SqlQuery<string>($"""
            SELECT ST_AsGeoJSON(geometry) AS "Value" FROM farm.farm_boundaries
            WHERE id = {approval.FarmBoundaryVersionId} AND tenant_id = {tenantId} AND farm_id = {proof.FarmId}
            """).SingleOrDefaultAsync(cancellationToken);
        var images = await db.Database.SqlQuery<Guid>($"""
            SELECT unnest(media_ids) AS "Value"
            FROM survey.v3_survey_releases
            WHERE id = {proof.PublicationId} AND tenant_id = {tenantId}
            """).ToListAsync(cancellationToken);

        IReadOnlyList<V3OwnerSurveyFinding> findings = [];
        IReadOnlyList<V3OwnerSurveyZone> zones = [];
        if (proof.ScreensDisease)
        {
            var review = await db.Set<SurveyComboReviewRevision>().AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == proof.ComboReviewRevisionId &&
                    r.SurveyOrderId == orderId && r.TenantId == tenantId && r.FarmId == proof.FarmId &&
                    r.DecisionFingerprint == approval.ComboReviewFingerprint, cancellationToken);
            var batch = await db.Set<SurveyComboImportBatch>().AsNoTracking()
                .SingleOrDefaultAsync(b => b.Id == approval.ComboBatchId &&
                    b.SurveyOrderId == orderId && b.TenantId == tenantId && b.FarmId == proof.FarmId,
                    cancellationToken);
            if (review is null || batch is null || review.ComboBatchId != batch.Id)
                return null;

            var decisions = review.DecisionsPayload.Deserialize<ComboReviewDecisionsSnapshotRecord>(PayloadOptions);
            var source = batch.RawPayload.Deserialize<ComboAnalysisReadyV1>(PayloadOptions);
            if (decisions is null || source is null)
                return null;

            var acceptedDecisionsByFindingId = decisions.FindingDecisions
                .Where(d => d.Decision == SurveyComboReviewRevision.FindingDecisionAccept)
                .ToDictionary(d => d.FindingId);
            var accepted = acceptedDecisionsByFindingId.Keys.ToHashSet();
            findings = source.Findings
                .Select(f => acceptedDecisionsByFindingId.TryGetValue(f.FindingId, out var decision)
                    ? (Finding: f, EffectiveProfileId: decision.ReviewedPlantProfileId ?? f.PlantProfileId)
                    : default)
                .Where(x => x.Finding is not null && x.EffectiveProfileId.HasValue)
                .Select(x => new V3OwnerSurveyFinding(x.Finding.FindingId, x.EffectiveProfileId!.Value, x.Finding.Position,
                    x.Finding.ObservedAt, x.Finding.SuspectedClassCode, x.Finding.Confidence, x.Finding.Uncertainty,
                    x.Finding.Limitations)).ToArray();

            var approvedZones = decisions.ZoneDecisions
                .Where(z => z.Decision is SurveyComboReviewRevision.ZoneDecisionApprove or SurveyComboReviewRevision.ZoneDecisionAdjust)
                .ToDictionary(z => z.SourceProposalId, StringComparer.Ordinal);
            zones = source.DiseaseZones.Where(z => approvedZones.ContainsKey(z.SourceProposalId))
                .Select(z =>
                {
                    var decision = approvedZones[z.SourceProposalId];
                    return new V3OwnerSurveyZone(z.SourceProposalId, z.SuspectedClassCode,
                        decision.ReviewedGeometry ?? z.Geometry,
                        (decision.ReviewedFindingIds ?? z.Memberships.Select(m => m.FindingId).ToArray())
                            .Where(accepted.Contains).ToArray(),
                        decision.SelectedGuidanceId, decision.SelectedGuidanceVersionNumber);
                }).ToArray();
        }

        var count = await db.Set<SurveyInvoiceRevision>().AsNoTracking()
            .Where(i => i.Id == proof.InvoiceRevisionId && i.SurveyOrderId == orderId &&
                i.EvidenceFingerprint == proof.InvoiceFingerprint)
            .Select(i => i.VerifiedBillablePoleCount).SingleAsync(cancellationToken);
        return new V3OwnerSurveyResult(Summary(proof), count, boundary, poles, approval.HealthAssessmentStatus,
            approval.LimitationNotice, findings, zones, images);
    }

    public async Task<V3OwnerSurveyComparison?> CompareAsync(
        Guid tenantId, Guid farmId, Guid fromOrderId, Guid toOrderId, CancellationToken cancellationToken)
    {
        var from = await GetAsync(tenantId, fromOrderId, cancellationToken);
        var to = await GetAsync(tenantId, toOrderId, cancellationToken);
        if (from is null || to is null || from.Summary.FarmId != farmId || to.Summary.FarmId != farmId ||
            from.Summary.BaselineMapVersionId != to.Summary.BaselineMapVersionId)
            return null;

        var previous = from.Poles.ToDictionary(p => p.PlantProfileId);
        var current = to.Poles.ToDictionary(p => p.PlantProfileId);
        var added = to.Poles.Where(p => !previous.ContainsKey(p.PlantProfileId)).ToArray();
        var missing = from.Poles.Where(p => !current.ContainsKey(p.PlantProfileId)).ToArray();
        var changed = to.Poles.Where(p => previous.TryGetValue(p.PlantProfileId, out var old) && old != p)
            .Select(p => new V3OwnerSurveyPoleChange(previous[p.PlantProfileId], p)).ToArray();
        var health = new List<V3OwnerSurveyHealthChange>();
        // MAP carries no health observation, so a MAP comparison cannot clear a COMBO finding.
        if (from.Summary.ServiceCode == SurveyInternalApprovedResultRevision.ServiceCombo &&
            to.Summary.ServiceCode == SurveyInternalApprovedResultRevision.ServiceCombo)
        {
            var fromClasses = SuspectedClasses(from.Findings);
            var toClasses = SuspectedClasses(to.Findings);
            foreach (var id in previous.Keys.Intersect(current.Keys).Order())
            {
                var before = fromClasses.GetValueOrDefault(id, []);
                var after = toClasses.GetValueOrDefault(id, []);
                if (!before.SequenceEqual(after))
                    health.Add(new V3OwnerSurveyHealthChange(id, before, after));
            }
        }

        return new V3OwnerSurveyComparison(from.Summary, to.Summary, added, missing, changed, health);
    }

    public async Task<Uri?> GetImageAsync(
        Guid tenantId, Guid orderId, Guid mediaId, CancellationToken cancellationToken)
    {
        var proof = await ReadVisibleProofAsync(tenantId, orderId, cancellationToken);
        if (proof is null)
            return null;

        var key = await db.Database.SqlQuery<string>($"""
            SELECT a.url AS "Value"
            FROM survey.v3_survey_releases r
            JOIN media.media_assets a ON a.id = {mediaId} AND a.id = ANY(r.media_ids)
                AND a.tenant_id = r.tenant_id AND a.farm_id = r.farm_id
            WHERE r.id = {proof.PublicationId} AND r.tenant_id = {tenantId}
              AND a.deleted_at IS NULL AND a.storage_status = 'ACTIVE'
              AND NOT EXISTS (SELECT 1 FROM survey.quality_refund_cases c
                  WHERE c.v3_publication_id = r.id AND c.status = 'Refunded')
            """).SingleOrDefaultAsync(cancellationToken);

        return key is null ? null : await storage.CreateDownloadUriAsync(key, TimeSpan.FromMinutes(2), cancellationToken);
    }

    private async Task<V3ResultReleaseProof?> ReadVisibleProofAsync(
        Guid tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var proof = await releases.GetByOrderIdAsync(tenantId, orderId, cancellationToken);
        return proof is not null && proof.AllowsOwnerActiveAccess(tenantId, proof.FarmId) &&
            await FarmAvailableAsync(tenantId, proof.FarmId, cancellationToken)
            ? proof
            : null;
    }

    private Task<bool> FarmAvailableAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken) =>
        db.Set<Farm>().AsNoTracking().AnyAsync(f => f.Id == farmId && f.TenantId == tenantId &&
            f.Status == GeneralStatus.Active && f.DeletedAt == null, cancellationToken);

    private async Task<IReadOnlyList<V3OwnerSurveyPole>> ReadPolesAsync(
        V3ResultReleaseProof proof, Guid snapshotId, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<V3OwnerSurveyPole>($"""
            SELECT DISTINCT ON (i.plant_profile_id)
                i.plant_profile_id AS "PlantProfileId", p.plant_code AS "PlantCode",
                ST_X(i.effective_position) AS "Longitude", ST_Y(i.effective_position) AS "Latitude",
                i.effective_row_index AS "Row", i.effective_column_index AS "Column"
            FROM survey.survey_order_pole_count_snapshot_items i
            JOIN plant.plants p ON p.plant_profile_id = i.plant_profile_id AND p.farm_id = i.farm_id
            WHERE i.snapshot_id = {snapshotId} AND i.tenant_id = {proof.TenantId} AND i.farm_id = {proof.FarmId}
              AND i.is_accepted AND i.is_strictly_in_boundary AND p.created_at <= {proof.PublishedAt}
            ORDER BY i.plant_profile_id, p.created_at DESC, p.id, i.id
            """).ToListAsync(cancellationToken);

    private static V3OwnerSurveySummary Summary(V3ResultReleaseProof proof) =>
        new(proof.OrderId, proof.PublicationId, proof.FarmId, proof.ServiceCode,
            proof.FarmBaseMapVersionId, proof.BaselineMapVersionId, proof.PublishedAt, proof.ClaimDeadlineExclusiveUtc);

    private static Dictionary<Guid, string[]> SuspectedClasses(IReadOnlyList<V3OwnerSurveyFinding> findings) =>
        findings.GroupBy(f => f.PlantProfileId).ToDictionary(g => g.Key,
            g => g.Select(f => f.SuspectedClassCode).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
}
