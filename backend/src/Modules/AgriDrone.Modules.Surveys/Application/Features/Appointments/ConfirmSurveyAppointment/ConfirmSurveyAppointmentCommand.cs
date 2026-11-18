using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Surveys.Domain;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;

public sealed record ConfirmSurveyAppointmentCommand(
    Guid SurveyOrderId,
    Guid AppointmentId,
    uint ExpectedOrderVersion,
    uint ExpectedAppointmentVersion,
    ContractScope ExpectedScope = ContractScope.Legacy)
    : IRequest<Result<ConfirmSurveyAppointmentResponse>>;
