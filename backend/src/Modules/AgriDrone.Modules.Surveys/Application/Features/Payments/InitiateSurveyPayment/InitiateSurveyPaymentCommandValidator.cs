using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;

internal sealed class InitiateSurveyPaymentCommandValidator
    : AbstractValidator<InitiateSurveyPaymentCommand>
{
    public InitiateSurveyPaymentCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
    }
}
