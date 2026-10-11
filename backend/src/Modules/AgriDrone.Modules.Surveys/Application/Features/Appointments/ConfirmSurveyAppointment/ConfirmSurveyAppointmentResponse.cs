using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;

public sealed record ConfirmSurveyAppointmentResponse(
    Guid SurveyOrderId,
    Guid AppointmentId,
    SurveyAppointmentPurpose Purpose,
    int RevisionNumber,
    Guid? PreviousAppointmentId,
    DateTimeOffset ProposedStartAt,
    DateTimeOffset ProposedEndAt,
    SurveyAppointmentStatus AppointmentStatus,
    uint AppointmentVersion,
    SurveyOrderStatus OrderStatus,
    uint OrderVersion,
    Guid ConfirmedBy,
    DateTimeOffset ConfirmedAt);
