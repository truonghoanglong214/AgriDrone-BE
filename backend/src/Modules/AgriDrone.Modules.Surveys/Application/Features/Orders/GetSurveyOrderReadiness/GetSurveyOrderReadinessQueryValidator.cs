using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;

internal sealed class GetSurveyOrderReadinessQueryValidator
    : AbstractValidator<GetSurveyOrderReadinessQuery>
{
    public GetSurveyOrderReadinessQueryValidator()
    {
        RuleFor(query => query.SurveyOrderId).NotEmpty();
        RuleFor(query => query.MissionId).NotEmpty();
        RuleFor(query => query.Purpose).IsInEnum();
    }
}
