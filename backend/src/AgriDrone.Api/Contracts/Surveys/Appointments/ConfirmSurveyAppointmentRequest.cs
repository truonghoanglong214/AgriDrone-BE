using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Appointments;

public sealed record ConfirmSurveyAppointmentRequest(
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAppointmentVersion);
