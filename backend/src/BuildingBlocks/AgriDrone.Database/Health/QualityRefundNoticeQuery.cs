using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal sealed class QualityRefundNoticeQuery(SurveyResultPublicationDbContext db) : IQualityRefundNoticeQuery
{
    public Task<DateTimeOffset?> GetCreatedAtAsync(Guid tenantId, Guid orderId, Guid resultId, CancellationToken cancellationToken) =>
        (from result in db.SurveyResults.AsNoTracking()
         join publication in db.HealthPublications.AsNoTracking() on result.Id equals publication.SurveyResultId
         join notice in db.HealthPublicationNotifications.AsNoTracking() on (Guid?)publication.Id equals notice.SourcePublicationId
         where result.Id == resultId && result.SurveyOrderId == orderId && result.TenantId == tenantId &&
             publication.TenantId == tenantId && notice.TenantId == tenantId &&
             publication.FarmId == result.FarmId && notice.FarmId == result.FarmId &&
             publication.BatchId == result.ApprovedBatchId && publication.ApprovalFingerprint == result.ApprovalFingerprint &&
             notice.NotificationType == "PLANT_HEALTH_PUBLISHED"
         select (DateTimeOffset?)notice.CreatedAt).MinAsync(cancellationToken);
}
