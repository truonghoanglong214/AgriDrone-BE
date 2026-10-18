using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.FailAdjustmentSettlement;

public sealed class FailAdjustmentSettlementCommandValidator
    : AbstractValidator<FailAdjustmentSettlementCommand>
{
    public FailAdjustmentSettlementCommandValidator()
    {
        RuleFor(x => x.SettlementId).NotEmpty();
        RuleFor(x => x.FailureReason).NotEmpty().MaximumLength(2000);
    }
}
