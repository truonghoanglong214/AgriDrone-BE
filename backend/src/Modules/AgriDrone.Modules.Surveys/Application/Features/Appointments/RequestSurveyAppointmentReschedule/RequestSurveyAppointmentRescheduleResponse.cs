using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;

public sealed record RequestSurveyAppointmentRescheduleResponse(
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
    string Reason,
    DateTimeOffset RequestedAt);
