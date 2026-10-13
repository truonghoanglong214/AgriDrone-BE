using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;

public sealed record RequestSurveyAppointmentRescheduleCommand(
    Guid SurveyOrderId,
    Guid AppointmentId,
    string Reason,
    uint ExpectedOrderVersion,
    uint ExpectedAppointmentVersion)
    : IRequest<Result<RequestSurveyAppointmentRescheduleResponse>>;
