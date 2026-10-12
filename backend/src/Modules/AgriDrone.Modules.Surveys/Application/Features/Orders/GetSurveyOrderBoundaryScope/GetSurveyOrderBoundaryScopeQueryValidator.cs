using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderBoundaryScope;

internal sealed class GetSurveyOrderBoundaryScopeQueryValidator
    : AbstractValidator<GetSurveyOrderBoundaryScopeQuery>
{
    public GetSurveyOrderBoundaryScopeQueryValidator()
    {
        RuleFor(query => query.SurveyOrderId).NotEmpty();
    }
}
