using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateBalanceCheckout;

internal sealed class InitiateBalanceCheckoutCommandValidator
    : AbstractValidator<InitiateBalanceCheckoutCommand>
{
    public InitiateBalanceCheckoutCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty();
        RuleFor(command => command.CheckoutToken)
            .NotEmpty()
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .MaximumLength(8192);
    }
}
