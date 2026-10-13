using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ApplyPriceAdjustment;

internal sealed class ApplyPriceAdjustmentCommandValidator
    : AbstractValidator<ApplyPriceAdjustmentCommand>
{
    public ApplyPriceAdjustmentCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.PriceAdjustmentId).NotEmpty();
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(command => command.ExpectedAdjustmentVersion).GreaterThan(0u);
        RuleFor(command => command.ExpectedPaymentVersion)
            .GreaterThan(0u)
            .When(command => command.ExpectedPaymentVersion.HasValue);
    }
}
