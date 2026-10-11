using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Api.Mapping;

internal static class SurveyAppointmentResponseMapper
{
    public static SurveyAppointmentPurpose ToDomain(
        SurveyAppointmentPurposeValue purpose) => purpose switch
        {
            SurveyAppointmentPurposeValue.BaselineMapping =>
                SurveyAppointmentPurpose.BaselineMapping,
            SurveyAppointmentPurposeValue.PaidService =>
                SurveyAppointmentPurpose.PaidService,
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };

    public static SurveyAppointmentApiResponse ToResponse(
        ProposeSurveyAppointmentResponse response) => new(
            response.SurveyOrderId,
            response.AppointmentId,
            MapPurpose(response.Purpose),
            response.RevisionNumber,
            response.PreviousAppointmentId,
            response.ProposedStartAt,
            response.ProposedEndAt,
            MapAppointmentStatus(response.Status),
            response.AppointmentVersion,
            "AWAITING_BASELINE_APPOINTMENT",
            response.OrderVersion,
            null,
            null,
            null,
            response.ProposedAt);

    public static SurveyAppointmentApiResponse ToResponse(
        ConfirmSurveyAppointmentResponse response) => new(
            response.SurveyOrderId,
            response.AppointmentId,
            MapPurpose(response.Purpose),
            response.RevisionNumber,
            response.PreviousAppointmentId,
            response.ProposedStartAt,
            response.ProposedEndAt,
            MapAppointmentStatus(
                response.AppointmentStatus),
            response.AppointmentVersion,
            MapOrderStatus(response.OrderStatus),
            response.OrderVersion,
            response.ConfirmedBy,
            response.ConfirmedAt,
            null,
            response.ConfirmedAt);

    public static SurveyAppointmentApiResponse ToResponse(
        RequestSurveyAppointmentRescheduleResponse response) => new(
            response.SurveyOrderId,
            response.AppointmentId,
            MapPurpose(response.Purpose),
            response.RevisionNumber,
            response.PreviousAppointmentId,
            response.ProposedStartAt,
            response.ProposedEndAt,
            MapAppointmentStatus(
                response.AppointmentStatus),
            response.AppointmentVersion,
            MapOrderStatus(response.OrderStatus),
            response.OrderVersion,
            null,
            null,
            response.Reason,
            response.RequestedAt);

    public static SurveyOrderReadinessApiResponse ToResponse(
        SurveyOrderReadinessResponse response) => new(
            response.SurveyOrderId,
            response.MissionId,
            response.TenantId,
            response.FarmId,
            response.RequestedMissionPurpose,
            response.OrderVersion,
            response.AppointmentId,
            response.SystemManagerProfileId,
            response.IsReady,
            response.ReadinessFailures,
            response.EvaluatedAt);

    private static string MapOrderStatus(SurveyOrderStatus status) =>
        status switch
        {
            SurveyOrderStatus.AwaitingBaselineAppointment =>
                "AWAITING_BASELINE_APPOINTMENT",
            SurveyOrderStatus.BaselineReady => "BASELINE_READY",
            _ => status.ToString().ToUpperInvariant()
        };

    private static string MapPurpose(SurveyAppointmentPurpose purpose) =>
        purpose switch
        {
            SurveyAppointmentPurpose.BaselineMapping => "BASELINE_MAPPING",
            SurveyAppointmentPurpose.PaidService => "PAID_SERVICE",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };

    private static string MapAppointmentStatus(
        SurveyAppointmentStatus status) => status switch
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
