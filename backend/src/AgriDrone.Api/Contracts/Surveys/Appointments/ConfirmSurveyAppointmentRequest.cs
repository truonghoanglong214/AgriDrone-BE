using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record ConfirmSurveyAppointmentRequest(
    [property: Required] uint? ExpectedOrderVersion,
    [property: Required] uint? ExpectedAppointmentVersion);
