using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestDetail;

internal sealed class GetSurveyRequestDetailQueryValidator
    : AbstractValidator<GetSurveyRequestDetailQuery>
{
    public GetSurveyRequestDetailQueryValidator()
    {
        RuleFor(query => query.SurveyRequestId).NotEmpty();
    }
}
