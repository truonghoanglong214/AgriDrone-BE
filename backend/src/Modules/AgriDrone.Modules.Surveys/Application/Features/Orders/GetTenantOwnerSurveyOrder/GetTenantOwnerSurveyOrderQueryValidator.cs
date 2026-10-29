using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetTenantOwnerSurveyOrder;

internal sealed class GetTenantOwnerSurveyOrderQueryValidator
    : AbstractValidator<GetTenantOwnerSurveyOrderQuery>
{
    public GetTenantOwnerSurveyOrderQueryValidator()
    {
        RuleFor(query => query.SurveyOrderId).NotEmpty();
    }
}
