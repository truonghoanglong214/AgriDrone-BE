using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record RequestSurveyAppointmentRescheduleRequest(
    [param: Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAppointmentVersion);
