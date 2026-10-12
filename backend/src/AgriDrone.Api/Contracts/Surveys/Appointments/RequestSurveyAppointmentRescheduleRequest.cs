using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record RequestSurveyAppointmentRescheduleRequest(
    [property: Required, StringLength(1000, MinimumLength = 1)] string Reason,
    [property: Required] uint? ExpectedOrderVersion,
    [property: Required] uint? ExpectedAppointmentVersion);
