using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.RejectPriceAdjustment;

internal sealed class RejectPriceAdjustmentCommandValidator
    : AbstractValidator<RejectPriceAdjustmentCommand>
{
    public RejectPriceAdjustmentCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.PriceAdjustmentId).NotEmpty();
        RuleFor(command => command.Reason)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(2000);
        RuleFor(command => command.EvidenceReference)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(1000);
        RuleFor(command => command.ExpectedOrderVersion).GreaterThan(0u);
        RuleFor(command => command.ExpectedAdjustmentVersion).GreaterThan(0u);
    }
}
