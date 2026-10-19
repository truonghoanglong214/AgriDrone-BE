using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.RequestScopeAdjustment;

public sealed class RequestScopeAdjustmentCommandValidator
    : AbstractValidator<RequestScopeAdjustmentCommand>
{
    public RequestScopeAdjustmentCommandValidator()
    {
        RuleFor(x => x.SurveyOrderId).NotEmpty();
        RuleFor(x => x.EvidenceReference).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.BeforePoleCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AfterPoleCount).GreaterThanOrEqualTo(0);
    }
}
