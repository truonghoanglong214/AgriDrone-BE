using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.Common;

internal static class SurveyAppointmentCommandSupport
{
    public static AppError MapDomainError(SurveyDomainException exception) =>
        exception.Code switch
        {
            AppointmentDomainErrorCodes.VersionConflict =>
                SurveyAppointmentError.VersionConflict(),
            AppointmentDomainErrorCodes.InvalidTransition =>
                SurveyAppointmentError.InvalidTransition(exception.Message),
            AppointmentDomainErrorCodes.ConfirmationWindowExpired =>
                SurveyAppointmentError.ConfirmationWindowExpired(),
            SurveyOrderDomainErrorCodes.VersionConflict =>
                SurveyOrderError.VersionConflict(),
            SurveyOrderDomainErrorCodes.InvalidTransition =>
                SurveyOrderError.InvalidTransition(exception.Message),
            _ => AppError.Conflict(exception.Code, exception.Message)
        };

    public static SurveyAppointmentChangedV1 CreateEvent(
        SurveyOrder order,
        SurveyAppointment appointment,
        DateTimeOffset changedAt) => new(
            order.Id,
            appointment.Id,
            order.TenantId,
            order.FarmId,
            MapPurpose(appointment.Purpose),
            appointment.RevisionNumber,
            MapStatus(appointment.Status),
            appointment.ProposedStartAt,
            appointment.ProposedEndAt,
            appointment.PreviousAppointmentId,
            changedAt);

    public static string MapPurpose(SurveyAppointmentPurpose purpose) =>
        purpose switch
        {
            SurveyAppointmentPurpose.BaselineMapping => "BASELINE_MAPPING",
            SurveyAppointmentPurpose.PaidService => "PAID_SERVICE",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };

    public static string MapStatus(SurveyAppointmentStatus status) =>
        status switch
        {
            SurveyAppointmentStatus.Proposed => "PROPOSED",
            SurveyAppointmentStatus.Confirmed => "CONFIRMED",
            SurveyAppointmentStatus.RescheduleRequested =>
                "RESCHEDULE_REQUESTED",
            SurveyAppointmentStatus.Cancelled => "CANCELLED",
            SurveyAppointmentStatus.Superseded => "SUPERSEDED",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
}
