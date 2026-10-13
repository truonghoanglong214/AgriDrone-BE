using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ActivateSurveyService;

internal sealed class ActivateSurveyServiceCommandValidator
    : AbstractValidator<ActivateSurveyServiceCommand>
{
    public ActivateSurveyServiceCommandValidator()
    {
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
    }
}
