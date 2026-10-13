using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.StartSurveyRequestReview;

internal sealed class StartSurveyRequestReviewCommandValidator
    : AbstractValidator<StartSurveyRequestReviewCommand>
{
    public StartSurveyRequestReviewCommandValidator()
    {
        RuleFor(command => command.SurveyRequestId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
    }
}
