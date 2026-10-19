using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.ReviewScopeAdjustment;

public sealed class ReviewScopeAdjustmentCommandValidator
    : AbstractValidator<ReviewScopeAdjustmentCommand>
{
    public ReviewScopeAdjustmentCommandValidator()
    {
        RuleFor(x => x.SurveyOrderId).NotEmpty();
        RuleFor(x => x.ScopeAdjustmentId).NotEmpty();
        RuleFor(x => x.ReviewReason).NotEmpty().MaximumLength(2000);
    }
}
