using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServicePrice;

internal sealed class CreateSurveyServicePriceCommandValidator
    : AbstractValidator<CreateSurveyServicePriceCommand>
{
    public CreateSurveyServicePriceCommandValidator()
    {
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.AmountPerPole)
            .GreaterThanOrEqualTo(0.01m)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(3)
            .Must(currency => string.Equals(
                currency.Trim(),
                "VND",
                StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only VND per-pole prices are supported.");
        RuleFor(command => command.ExpectedServiceVersion).GreaterThan(0u);
    }
}
