using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServicePriceHistory;

internal sealed class GetSurveyServicePriceHistoryQueryValidator
    : AbstractValidator<GetSurveyServicePriceHistoryQuery>
{
    public GetSurveyServicePriceHistoryQueryValidator()
    {
        RuleFor(query => query.SurveyServiceId).NotEmpty();
    }
}
