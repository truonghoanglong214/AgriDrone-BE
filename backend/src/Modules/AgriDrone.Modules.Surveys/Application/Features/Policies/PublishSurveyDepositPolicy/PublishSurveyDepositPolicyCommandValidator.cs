using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishSurveyDepositPolicy;

internal sealed class PublishSurveyDepositPolicyCommandValidator
    : AbstractValidator<PublishSurveyDepositPolicyCommand>
{
    public PublishSurveyDepositPolicyCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .Must(amount => amount == decimal.Floor(amount))
            .WithMessage("Deposit policy amount must be a positive whole integer for VND.");
        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3)
            .Must(currency => string.Equals(
                currency.Trim(),
                "VND",
                StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only VND currency is supported for survey deposit policies.");
        RuleFor(x => x.ExpectedLatestVersion)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExpectedLatestVersion.HasValue);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EvidenceReference).MaximumLength(500);
    }
}
