using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record ProposeSurveyAppointmentRequest(
    [param: Required] SurveyAppointmentPurposeValue? Purpose,
    [param: Required] DateOnly? ScheduledDate = null,
    [param: Required] uint? ExpectedOrderVersion = null,
    Guid? ExpectedCurrentAppointmentId = null,
    uint? ExpectedCurrentAppointmentVersion = null);

public enum SurveyAppointmentPurposeValue
{
    BaselineMapping,
    PaidService
}
