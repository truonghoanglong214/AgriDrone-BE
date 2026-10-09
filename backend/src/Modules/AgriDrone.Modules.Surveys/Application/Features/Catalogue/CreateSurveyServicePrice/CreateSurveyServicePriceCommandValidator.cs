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
        RuleFor(command => command.EffectiveFrom)
            .Must(IsUtc)
            .WithMessage("EffectiveFrom must be a non-default UTC timestamp.");
        RuleFor(command => command.EffectiveTo)
            .Must(value => !value.HasValue || IsUtc(value.Value))
            .WithMessage("EffectiveTo must be a UTC timestamp when provided.")
            .Must((command, effectiveTo) =>
                !effectiveTo.HasValue || effectiveTo > command.EffectiveFrom)
            .WithMessage("EffectiveTo must be later than EffectiveFrom.");
        RuleFor(command => command.ExpectedServiceVersion).GreaterThan(0u);
    }

    private static bool IsUtc(DateTimeOffset value) =>
        value != default && value.Offset == TimeSpan.Zero;
}
