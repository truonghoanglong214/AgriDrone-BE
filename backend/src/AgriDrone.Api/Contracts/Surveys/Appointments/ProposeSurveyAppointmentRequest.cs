using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record ProposeSurveyAppointmentRequest(
    [property: Required] SurveyAppointmentPurposeValue? Purpose,
    [property: Required] DateTimeOffset? ProposedStartAt,
    [property: Required] DateTimeOffset? ProposedEndAt,
    [property: Required] uint? ExpectedOrderVersion,
    Guid? ExpectedCurrentAppointmentId,
    uint? ExpectedCurrentAppointmentVersion);

[JsonConverter(typeof(JsonStringEnumConverter<SurveyAppointmentPurposeValue>))]
public enum SurveyAppointmentPurposeValue
{
    BaselineMapping,
    PaidService
}
