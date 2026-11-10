using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

public sealed class CreateSurveyComboDiscountPolicyCommandValidator
    : AbstractValidator<CreateSurveyComboDiscountPolicyCommand>
{
    public CreateSurveyComboDiscountPolicyCommandValidator()
    {
        RuleFor(command => command.DiscountPercentage)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Discount percentage must be between 0 and 100.");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Expected version cannot be negative.");
    }
}

