using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.ApplyScopeAdjustment;

public sealed class ApplyScopeAdjustmentCommandValidator
    : AbstractValidator<ApplyScopeAdjustmentCommand>
{
    public ApplyScopeAdjustmentCommandValidator()
    {
        RuleFor(x => x.SurveyOrderId).NotEmpty();
        RuleFor(x => x.ScopeAdjustmentId).NotEmpty();
    }
}
