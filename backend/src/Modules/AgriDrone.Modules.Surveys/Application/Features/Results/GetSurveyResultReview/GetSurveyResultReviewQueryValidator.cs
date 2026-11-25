using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.GetSurveyResultReview;

internal sealed class GetSurveyResultReviewQueryValidator : AbstractValidator<GetSurveyResultReviewQuery>
{
    public GetSurveyResultReviewQueryValidator()
    {
        RuleFor(q => q.OrderId).NotEmpty();
    }
}
