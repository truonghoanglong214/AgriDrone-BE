using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;

public sealed record ConfirmSurveyAppointmentCommand(
    Guid SurveyOrderId,
    Guid AppointmentId,
    uint ExpectedOrderVersion,
    uint ExpectedAppointmentVersion)
    : IRequest<Result<ConfirmSurveyAppointmentResponse>>;
