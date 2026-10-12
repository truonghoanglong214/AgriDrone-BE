namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record SurveyAppointmentApiResponse(
    Guid SurveyOrderId,
    Guid AppointmentId,
    string Purpose,
    int RevisionNumber,
    Guid? PreviousAppointmentId,
    DateTimeOffset ProposedStartAt,
    DateTimeOffset ProposedEndAt,
    string AppointmentStatus,
    uint AppointmentVersion,
    string OrderStatus,
    uint OrderVersion,
    Guid? ConfirmedBy,
    DateTimeOffset? ConfirmedAt,
    string? RescheduleReason,
    DateTimeOffset ChangedAt);
