using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServiceV3;

internal sealed class CreateSurveyServiceV3CommandValidator : AbstractValidator<CreateSurveyServiceV3Command>
{
    public CreateSurveyServiceV3CommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
    }
}
