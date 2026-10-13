using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;

internal sealed class ConfirmSurveyAppointmentCommandValidator
    : AbstractValidator<ConfirmSurveyAppointmentCommand>
{
    public ConfirmSurveyAppointmentCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.AppointmentId).NotEmpty();
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(command => command.ExpectedAppointmentVersion).GreaterThan(0u);
    }
}
