using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.RetireSurveyService;

internal sealed class RetireSurveyServiceCommandValidator
    : AbstractValidator<RetireSurveyServiceCommand>
{
    public RetireSurveyServiceCommandValidator()
    {
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
    }
}
