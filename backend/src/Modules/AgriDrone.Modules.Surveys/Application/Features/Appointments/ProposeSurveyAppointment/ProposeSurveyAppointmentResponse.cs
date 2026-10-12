using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;

public sealed record ProposeSurveyAppointmentResponse(
    Guid SurveyOrderId,
    Guid AppointmentId,
    SurveyAppointmentPurpose Purpose,
    int RevisionNumber,
    Guid? PreviousAppointmentId,
    DateTimeOffset ProposedStartAt,
    DateTimeOffset ProposedEndAt,
    SurveyAppointmentStatus Status,
    uint AppointmentVersion,
    SurveyOrderStatus OrderStatus,
    uint OrderVersion,
    DateTimeOffset ProposedAt);
