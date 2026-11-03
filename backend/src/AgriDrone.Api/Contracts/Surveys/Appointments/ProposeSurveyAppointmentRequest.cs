using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record ProposeSurveyAppointmentRequest(
    [param: Required] SurveyAppointmentPurposeValue? Purpose,
    DateTimeOffset? ProposedStartAt = null,
    DateTimeOffset? ProposedEndAt = null,
    DateOnly? ScheduledDate = null,
    DateOnly? ScheduledStartDate = null,
    DateOnly? ScheduledEndDate = null,
    [param: Required] uint? ExpectedOrderVersion = null,
    Guid? ExpectedCurrentAppointmentId = null,
    uint? ExpectedCurrentAppointmentVersion = null);

public enum SurveyAppointmentPurposeValue
{
    BaselineMapping,
    PaidService
}
