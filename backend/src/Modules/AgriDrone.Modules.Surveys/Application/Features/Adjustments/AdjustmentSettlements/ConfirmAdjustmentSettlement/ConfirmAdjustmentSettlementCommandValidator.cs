using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.ConfirmAdjustmentSettlement;

public sealed class ConfirmAdjustmentSettlementCommandValidator
    : AbstractValidator<ConfirmAdjustmentSettlementCommand>
{
    public ConfirmAdjustmentSettlementCommandValidator()
    {
        RuleFor(x => x.SettlementId).NotEmpty();
    }
}
