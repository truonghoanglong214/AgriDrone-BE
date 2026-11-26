using System.Text.Json;
using AgriDrone.IntegrationContracts.Surveys;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class V3SurveyReleaseStore(AgriDroneSchemaDbContext db)
    : IV3ResultReleaseProofReader, IV3BaselineDeliveryProofReader
{
    public Task<V3ResultReleaseProof?> GetByOrderIdAsync(
        Guid tenantId, Guid orderId, CancellationToken cancellationToken = default) =>
        ReadProofAsync(tenantId, orderId, null, cancellationToken);

    public Task<V3ResultReleaseProof?> GetByPublicationIdAsync(
        Guid tenantId, Guid publicationId, CancellationToken cancellationToken = default) =>
        ReadProofAsync(tenantId, null, publicationId, cancellationToken);

    private async Task<V3ResultReleaseProof?> ReadProofAsync(
        Guid tenantId, Guid? orderId, Guid? publicationId, CancellationToken cancellationToken)
    {
        var row = await db.Database.SqlQuery<ReleaseRow>($"""
            SELECT r.proof::text AS "ProofJson", refund.transferred_at AS "WithdrawnAt"
            FROM survey.v3_survey_releases r
            LEFT JOIN survey.quality_refund_cases refund
                ON refund.v3_publication_id = r.id AND refund.status = 'Refunded'
            WHERE r.tenant_id = {tenantId}
              AND ({orderId}::uuid IS NULL OR r.survey_order_id = {orderId}::uuid)
              AND ({publicationId}::uuid IS NULL OR r.id = {publicationId}::uuid)
            """).SingleOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var proof = JsonSerializer.Deserialize<V3ResultReleaseProof>(row.ProofJson)
            ?? throw new InvalidOperationException("The stored V3 publication proof is unavailable.");
        return proof with { IsWithdrawn = row.WithdrawnAt.HasValue, WithdrawnAt = row.WithdrawnAt };
    }

    public async Task<V3BaselineDeliveryProof?> ReadCurrentAsync(
        DbContext context, Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
        await context.Database.SqlQuery<V3BaselineDeliveryProof>($"""
            SELECT r.tenant_id AS "TenantId", r.farm_id AS "FarmId",
                r.map_id AS "BaseMapVersionId", r.survey_order_id AS "SourceOrderId",
                r.id AS "ReleaseId", true AS "IsReleased", true AS "FinanciallySettledAtRelease",
                EXISTS (SELECT 1 FROM survey.quality_refund_cases c
                    WHERE c.v3_publication_id = r.id AND c.status = 'Refunded') AS "IsWithdrawn"
            FROM survey.v3_survey_releases r
            JOIN farm.farm_base_map_versions m ON m.id = r.map_id
                AND m.source_survey_order_id = r.survey_order_id
                AND m.tenant_id = r.tenant_id AND m.farm_id = r.farm_id
            WHERE r.tenant_id = {tenantId} AND r.farm_id = {farmId} AND m.is_baseline
              AND m.status IN ('PUBLISHED', 'SUPERSEDED')
            ORDER BY r.published_at DESC, r.id DESC
            LIMIT 1
            """).FirstOrDefaultAsync(cancellationToken);

    internal async Task<EvaluateSurveySettlementReleaseResult?> ReadReceiptAsync(
        Guid orderId, Guid tenantId, CancellationToken cancellationToken)
    {
        var json = await db.Database.SqlQuery<string>($"""
            SELECT reconciliation_snapshot::text AS "Value"
            FROM survey.v3_survey_releases
            WHERE survey_order_id = {orderId} AND tenant_id = {tenantId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (json is null)
            return null;

        var receipt = JsonSerializer.Deserialize<EvaluateSurveySettlementReleaseResult>(json)
            ?? throw new InvalidOperationException("The stored V3 release receipt is unavailable.");
        var proof = await GetByOrderIdAsync(tenantId, orderId, cancellationToken);
        return receipt with { Outcome = SurveySettlementReleaseOutcome.Replay, ReleaseProof = proof };
    }

    internal Task InsertAsync(
        EvaluateSurveySettlementReleaseResult receipt, Guid notificationId,
        Guid[] mediaIds, CancellationToken cancellationToken)
    {
        var proof = receipt.ReleaseProof!;
        var json = JsonSerializer.Serialize(proof);
        var receiptJson = JsonSerializer.Serialize(receipt);
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO survey.v3_survey_releases
                (id, survey_order_id, tenant_id, farm_id, approval_id, invoice_id, map_id,
                 notification_id, published_at, media_ids, proof, reconciliation_snapshot)
            VALUES ({proof.PublicationId}, {proof.OrderId}, {proof.TenantId}, {proof.FarmId},
                {proof.ApprovedResultRevisionId}, {proof.InvoiceRevisionId}, {proof.FarmBaseMapVersionId},
                {notificationId}, {proof.PublishedAt}, {mediaIds}, {json}::jsonb, {receiptJson}::jsonb)
            """, cancellationToken);
    }

    private sealed record ReleaseRow(string ProofJson, DateTimeOffset? WithdrawnAt);
}
