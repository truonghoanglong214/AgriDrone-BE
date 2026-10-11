namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyAppointmentChangedV1(
    Guid SurveyOrderId,
    Guid AppointmentId,
    Guid TenantId,
    Guid FarmId,
    string Purpose,
    int RevisionNumber,
    string Status,
    DateTimeOffset ProposedStartAt,
    DateTimeOffset ProposedEndAt,
    Guid? PreviousAppointmentId,
    DateTimeOffset ChangedAt);
