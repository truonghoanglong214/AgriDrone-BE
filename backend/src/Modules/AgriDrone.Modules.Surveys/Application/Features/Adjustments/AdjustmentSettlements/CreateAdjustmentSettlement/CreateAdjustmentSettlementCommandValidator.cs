using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.CreateAdjustmentSettlement;

public sealed class CreateAdjustmentSettlementCommandValidator
    : AbstractValidator<CreateAdjustmentSettlementCommand>
{
    public CreateAdjustmentSettlementCommandValidator()
    {
        RuleFor(x => x.PriceAdjustmentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
