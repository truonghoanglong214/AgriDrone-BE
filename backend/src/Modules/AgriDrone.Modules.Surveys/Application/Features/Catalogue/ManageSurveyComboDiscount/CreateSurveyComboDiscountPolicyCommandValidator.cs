using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

public sealed class CreateSurveyComboDiscountPolicyCommandValidator
    : AbstractValidator<CreateSurveyComboDiscountPolicyCommand>
{
    public CreateSurveyComboDiscountPolicyCommandValidator()
    {
        RuleFor(command => command.DiscountPercentage)
            .GreaterThanOrEqualTo(0m).LessThan(100m)
            .PrecisionScale(5, 2, ignoreTrailingZeros: true)
            .WithMessage("Discount percentage must be at least 0 and less than 100, with at most two decimal places.");
        RuleFor(command => command.ExpectedVersion)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Expected version cannot be negative.");
    }
}

