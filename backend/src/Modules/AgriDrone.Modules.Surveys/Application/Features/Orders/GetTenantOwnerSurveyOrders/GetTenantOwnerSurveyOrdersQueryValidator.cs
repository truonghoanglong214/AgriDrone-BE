using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetTenantOwnerSurveyOrders;

internal sealed class GetTenantOwnerSurveyOrdersQueryValidator
    : AbstractValidator<GetTenantOwnerSurveyOrdersQuery>
{
    public GetTenantOwnerSurveyOrdersQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status)
            .Must(status => !status.HasValue || Enum.IsDefined(status.Value))
            .WithMessage("Status is not supported.");
    }
}
