using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Surveys.Domain;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;

public sealed record RequestSurveyAppointmentRescheduleCommand(
    Guid SurveyOrderId,
    Guid AppointmentId,
    string Reason,
    uint ExpectedOrderVersion,
    uint ExpectedAppointmentVersion,
    ContractScope ExpectedScope = ContractScope.Legacy)
    : IRequest<Result<RequestSurveyAppointmentRescheduleResponse>>;
