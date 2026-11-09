using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal sealed class PlantHealthPublicationQuery(SurveyResultPublicationDbContext db) : IPlantHealthPublicationQuery
{
    public async Task<PlantHealthPublishedProof?> GetAsync(Guid tenantId, Guid publicationId, CancellationToken token)
    {
        var item = await (from publication in db.HealthPublications.AsNoTracking()
            join result in db.SurveyResults.AsNoTracking() on publication.SurveyResultId equals result.Id
            join order in db.SurveyOrders.AsNoTracking() on result.SurveyOrderId equals order.Id
            join batch in db.HealthImportBatches.AsNoTracking() on publication.BatchId equals batch.Id
            where publication.Id == publicationId && publication.TenantId == tenantId &&
                result.TenantId == tenantId && order.TenantId == tenantId && batch.TenantId == tenantId &&
                publication.FarmId == result.FarmId && result.FarmId == order.FarmId && batch.FarmId == order.FarmId &&
                result.Status == SurveyResultStatus.Published && order.Status == SurveyOrderStatus.Completed &&
                result.ApprovedBatchId == batch.Id && result.ServiceType == SurveyServiceType.PlantHealth
            select new { publication.Payload, publication.Fingerprint, batch.OriginalPayload }).SingleOrDefaultAsync(token);
        if (item is null) return null;
        var payload = item.Payload.Deserialize<PlantHealthPreliminaryResultPublishedV1>()
            ?? throw new InvalidOperationException("Published Plant Health payload is missing.");
        if (PlantHealthV4Fingerprint.ForValue(payload) != item.Fingerprint)
            throw new InvalidOperationException("Published Plant Health fingerprint does not match its immutable payload.");
        var source = item.OriginalPayload.Deserialize<PlantHealthAnalysisReadyV4>()
            ?? throw new InvalidOperationException("Published Plant Health source is missing.");
        return new(payload, item.Fingerprint, source.Findings.Count);
    }
}
