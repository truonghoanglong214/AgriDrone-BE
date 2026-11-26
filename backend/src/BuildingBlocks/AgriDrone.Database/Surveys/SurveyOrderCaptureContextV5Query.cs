using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderCaptureContextV5Query(
    AgriDroneSchemaDbContext db,
    TimeProvider clock) : ISurveyOrderCaptureContextV5Query
{
    public async Task<SurveyOrderCaptureContextV5?> GetAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default)
    {
        var evaluatedAt = clock.GetUtcNow();
        var order = await db.Set<SurveyOrder>()
            .AsNoTracking()
            .Include(value => value.SurveyService)
            .SingleOrDefaultAsync(
                value => value.Id == surveyOrderId,
                cancellationToken);
        if (order is null)
        {
            return null;
        }

        var appointment = await db.Set<SurveyAppointment>()
            .AsNoTracking()
            .Where(value =>
                value.SurveyOrderId == order.Id &&
                value.Purpose == SurveyAppointmentPurpose.Capture &&
                value.Status != SurveyAppointmentStatus.Cancelled &&
                value.Status != SurveyAppointmentStatus.Superseded)
            .OrderByDescending(value => value.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var obligation = order.DepositObligationId.HasValue
            ? await db.Set<SurveyDepositObligation>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    value =>
                        value.Id == order.DepositObligationId.Value &&
                        value.SurveyOrderId == order.Id,
                    cancellationToken)
            : null;

        var attempt = await db.Set<CaptureAttempt>()
            .AsNoTracking()
            .Where(value => value.OrderId == order.Id &&
                value.Status != CaptureAttemptStatus.Accepted &&
                value.Status != CaptureAttemptStatus.ReflightRequired &&
                value.Status != CaptureAttemptStatus.FlightFailed &&
                value.Status != CaptureAttemptStatus.Cancelled)
            .OrderByDescending(value => value.AttemptIndex)
            .FirstOrDefaultAsync(cancellationToken);
        var executionMissionId = attempt is null
            ? null
            : await db.Set<DroneMission>()
                .AsNoTracking()
                .Where(value => value.CaptureAttemptId == attempt.Id)
                .Select(value => (Guid?)value.Id)
                .SingleOrDefaultAsync(cancellationToken);

        var blockers = new List<string>();
        if (order.Scope != ContractScope.V3)
        {
            blockers.Add("ORDER_SCOPE_NOT_V3");
        }
        if (order.Status != SurveyOrderStatus.ReadyForCapture)
        {
            blockers.Add("ORDER_NOT_READY_FOR_CAPTURE");
        }
        if (appointment is null ||
            appointment.Status != SurveyAppointmentStatus.Confirmed)
        {
            blockers.Add("CAPTURE_APPOINTMENT_NOT_CONFIRMED");
        }
        else if (evaluatedAt < appointment.ProposedStartAt ||
                 evaluatedAt >= appointment.ProposedEndAt)
        {
            blockers.Add("CAPTURE_APPOINTMENT_WINDOW_NOT_CURRENT");
        }
        if (obligation is null)
        {
            blockers.Add("DEPOSIT_OBLIGATION_MISSING");
        }
        else if (!obligation.IsSatisfied)
        {
            blockers.Add("DEPOSIT_NOT_SATISFIED");
        }
        if (attempt is not null)
        {
            blockers.Add("ACTIVE_CAPTURE_ATTEMPT_EXISTS");
        }
        if (!order.RequiresBaselineMapping &&
            (!order.BaselineMapVersionId.HasValue ||
             !order.FarmBaseMapVersionId.HasValue))
        {
            blockers.Add("FOLLOW_UP_BASELINE_NOT_PINNED");
        }

        return new SurveyOrderCaptureContextV5(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.Version,
            order.TenantId,
            order.FarmId,
            order.SurveyService.Code,
            order.SurveyService.ServiceType?.ToString() ?? order.SurveyService.Code,
            order.RequiresBaselineMapping,
            order.FarmBoundaryVersionId,
            order.FarmBaseMapVersionId,
            order.PreviousCompatibleOrderId,
            attempt?.Id,
            attempt?.AttemptIndex,
            attempt?.Status.ToString(),
            attempt?.Version,
            executionMissionId,
            appointment?.Id,
            appointment?.Purpose.ToString(),
            appointment?.Status.ToString(),
            appointment?.ProposedStartAt,
            appointment?.ProposedEndAt,
            appointment?.Version,
            obligation?.Id,
            obligation?.Amount ?? 0m,
            obligation?.Currency ?? "VND",
            obligation?.AllocatedAmount ?? 0m,
            obligation?.IsSatisfied ?? false,
            blockers.Count == 0,
            blockers,
            evaluatedAt);
    }
}
