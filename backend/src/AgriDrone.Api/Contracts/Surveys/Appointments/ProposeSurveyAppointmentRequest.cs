using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record ProposeSurveyAppointmentRequest(
    [param: Required] SurveyAppointmentPurposeValue? Purpose,
    [param: Required] DateTimeOffset? ProposedStartAt,
    [param: Required] DateTimeOffset? ProposedEndAt,
    [param: Required] uint? ExpectedOrderVersion,
    Guid? ExpectedCurrentAppointmentId,
    uint? ExpectedCurrentAppointmentVersion);

public enum SurveyAppointmentPurposeValue
{
    BaselineMapping,
    PaidService
}
