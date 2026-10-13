using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.MarkSurveyServiceExperimental;

internal sealed class MarkSurveyServiceExperimentalCommandValidator
    : AbstractValidator<MarkSurveyServiceExperimentalCommand>
{
    public MarkSurveyServiceExperimentalCommandValidator()
    {
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
    }
}
