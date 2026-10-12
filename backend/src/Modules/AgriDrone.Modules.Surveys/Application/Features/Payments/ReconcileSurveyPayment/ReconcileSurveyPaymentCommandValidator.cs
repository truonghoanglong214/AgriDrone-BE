using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;

internal sealed class ReconcileSurveyPaymentCommandValidator
    : AbstractValidator<ReconcileSurveyPaymentCommand>
{
    public ReconcileSurveyPaymentCommandValidator()
    {
        RuleFor(command => command.SurveyPaymentId).NotEmpty();
        RuleFor(command => command.ProviderReference)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(150);
        RuleFor(command => command.SettledAmount)
            .GreaterThan(0m)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(command => command.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$");
        RuleFor(command => command.Reason)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(2000);
        RuleFor(command => command.EvidenceReference)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(1000);
        RuleFor(command => command.ExpectedPaymentVersion).GreaterThan(0u);
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
    }
}
