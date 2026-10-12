using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;

internal sealed class RequestSurveyAppointmentRescheduleCommandValidator
    : AbstractValidator<RequestSurveyAppointmentRescheduleCommand>
{
    public RequestSurveyAppointmentRescheduleCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.AppointmentId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(command => command.ExpectedAppointmentVersion).GreaterThan(0u);
    }
}
