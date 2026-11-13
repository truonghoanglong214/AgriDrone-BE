using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.PublishSurveyServicePriceV3;

internal sealed class PublishSurveyServicePriceV3CommandValidator
    : AbstractValidator<PublishSurveyServicePriceV3Command>
{
    public PublishSurveyServicePriceV3CommandValidator()
    {
        RuleFor(x => x.SurveyServiceId).NotEmpty();
        RuleFor(x => x.AmountPerPole)
            .GreaterThan(0m)
            .Must(amount => amount == decimal.Floor(amount))
            .WithMessage("Amount per pole must be a positive whole integer for VND.");
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Must(currency => string.Equals(
                currency.Trim(),
                "VND",
                StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only VND currency is supported for V3 per-pole pricing.");
        RuleFor(x => x.ExpectedLatestPriceVersion)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExpectedLatestPriceVersion.HasValue);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EvidenceReference).MaximumLength(500);
    }
}
