using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.RequestPriceAdjustment;

internal sealed class RequestPriceAdjustmentCommandValidator
    : AbstractValidator<RequestPriceAdjustmentCommand>
{
    public RequestPriceAdjustmentCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.NewConfirmedPoleCount).GreaterThan(0);
        RuleFor(command => command.Reason)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(2000);
        RuleFor(command => command.EvidenceReference)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(1000);
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
    }
}
