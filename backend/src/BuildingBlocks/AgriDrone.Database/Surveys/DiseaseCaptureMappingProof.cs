using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal static class DiseaseCaptureMappingProof
{
    public static Task<bool> HasAcceptedSourceAsync(
        DbContext db, SurveyOrder order, SurveyOrderPoleCountSnapshot snapshot, CancellationToken cancellationToken) =>
        (from attempt in db.Set<CaptureAttempt>()
         join mission in db.Set<DroneMission>() on (Guid?)attempt.Id equals mission.CaptureAttemptId
         join acceptedCount in db.Set<SurveyOrderPoleCountSnapshot>() on attempt.Id equals acceptedCount.CaptureAttemptId
         where attempt.Id == snapshot.CaptureAttemptId && attempt.OrderId == order.Id &&
               attempt.TenantId == order.TenantId && attempt.FarmId == order.FarmId &&
               attempt.Status == CaptureAttemptStatus.Accepted && attempt.MappingSourceAttemptId == null &&
               acceptedCount.SurveyOrderId == order.Id && acceptedCount.TenantId == order.TenantId &&
               acceptedCount.FarmId == order.FarmId && acceptedCount.MissionId == snapshot.MissionId &&
               acceptedCount.FarmBaseMapVersionId == snapshot.FarmBaseMapVersionId &&
               acceptedCount.FarmBoundaryVersionId == snapshot.FarmBoundaryVersionId &&
               acceptedCount.MappingCandidateBatchId == snapshot.MappingCandidateBatchId &&
               acceptedCount.RevisionNumber <= snapshot.RevisionNumber &&
               attempt.ProcessingAcceptanceReceipt == acceptedCount.Id.ToString() &&
               mission.Id == snapshot.MissionId && mission.SurveyOrderId == order.Id &&
               mission.TenantId == order.TenantId && mission.FarmId == order.FarmId &&
               mission.Purpose == MissionPurpose.Capture
         select attempt.Id).AnyAsync(cancellationToken);

    public static bool MatchesDiseaseMission(
        SurveyOrderPoleCountSnapshot snapshot, DroneMission mission) =>
        mission.Id != snapshot.MissionId &&
        mission.Purpose == MissionPurpose.Capture &&
        mission.SourceMapVersionId == snapshot.FarmBaseMapVersionId &&
        mission.FarmBoundaryVersionId == snapshot.FarmBoundaryVersionId;

    public static async Task<bool> HasAcceptedDiseaseCaptureAsync(
        DbContext db, SurveyOrder order, SurveyOrderPoleCountSnapshot snapshot,
        SurveyInternalApprovedResultRevision approval, SurveyComboImportBatch combo, CancellationToken cancellationToken)
    {
        if (combo.CaptureAttemptId == approval.CaptureAttemptId)
            return combo.MissionId == approval.MissionId;

        var disease = await db.Set<CaptureAttempt>().AsNoTracking().SingleOrDefaultAsync(a =>
            a.Id == combo.CaptureAttemptId && a.OrderId == order.Id && a.TenantId == order.TenantId &&
            a.FarmId == order.FarmId && a.MappingSourceAttemptId == snapshot.CaptureAttemptId &&
            a.Status == CaptureAttemptStatus.Accepted, cancellationToken);
        if (disease is null || !Guid.TryParse(disease.ProcessingAcceptanceReceipt, out var receiptId) ||
            !Guid.TryParse(disease.InputApprovalReference, out var inputApprovalId) ||
            inputApprovalId != combo.InputApprovalId)
            return false;

        var mission = await db.Set<DroneMission>().AsNoTracking().SingleOrDefaultAsync(m =>
            m.Id == combo.MissionId && m.CaptureAttemptId == disease.Id && m.SurveyOrderId == order.Id &&
            m.TenantId == order.TenantId && m.FarmId == order.FarmId, cancellationToken);
        return mission is not null && MatchesDiseaseMission(snapshot, mission) &&
            await HasAcceptedSourceAsync(db, order, snapshot, cancellationToken) &&
            await db.Set<SurveyInternalApprovedResultRevision>().AnyAsync(r =>
                r.Id == receiptId && r.SurveyOrderId == order.Id && r.TenantId == order.TenantId &&
                r.FarmId == order.FarmId && r.CaptureAttemptId == snapshot.CaptureAttemptId &&
                r.MissionId == snapshot.MissionId && r.ComboBatchId == combo.Id &&
                r.RevisionNumber <= approval.RevisionNumber, cancellationToken);
    }
}
