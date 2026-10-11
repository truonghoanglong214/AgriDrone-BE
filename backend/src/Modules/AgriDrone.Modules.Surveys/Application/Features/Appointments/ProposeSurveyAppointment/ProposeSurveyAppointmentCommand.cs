using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;

public sealed record ProposeSurveyAppointmentCommand(
    Guid SurveyOrderId,
    SurveyAppointmentPurpose Purpose,
    DateTimeOffset ProposedStartAt,
    DateTimeOffset ProposedEndAt,
    uint ExpectedOrderVersion,
    Guid? ExpectedCurrentAppointmentId,
    uint? ExpectedCurrentAppointmentVersion)
    : IRequest<Result<ProposeSurveyAppointmentResponse>>;
