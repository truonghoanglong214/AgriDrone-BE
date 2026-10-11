using AgriDrone.Modules.Surveys.Domain;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;

internal sealed class ProposeSurveyAppointmentCommandValidator
    : AbstractValidator<ProposeSurveyAppointmentCommand>
{
    public ProposeSurveyAppointmentCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.Purpose)
            .Equal(SurveyAppointmentPurpose.BaselineMapping)
            .WithMessage("Phase 4B only supports BASELINE_MAPPING appointments.");
        RuleFor(command => command.ProposedStartAt)
            .Must(value => value != default && value.Offset == TimeSpan.Zero);
        RuleFor(command => command.ProposedEndAt)
            .Must(value => value != default && value.Offset == TimeSpan.Zero)
            .GreaterThan(command => command.ProposedStartAt);
        RuleFor(command => command.ExpectedCurrentAppointmentVersion)
            .NotNull()
            .When(command => command.ExpectedCurrentAppointmentId.HasValue);
        RuleFor(command => command.ExpectedCurrentAppointmentId)
            .NotNull()
            .When(command => command.ExpectedCurrentAppointmentVersion.HasValue);
    }
}
