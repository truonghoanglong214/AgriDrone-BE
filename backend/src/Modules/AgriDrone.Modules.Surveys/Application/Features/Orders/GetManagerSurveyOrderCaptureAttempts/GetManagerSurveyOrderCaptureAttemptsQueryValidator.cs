using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.GetManagerSurveyOrderCaptureAttempts;

internal sealed class GetManagerSurveyOrderCaptureAttemptsQueryValidator
    : AbstractValidator<GetManagerSurveyOrderCaptureAttemptsQuery>
{
    public GetManagerSurveyOrderCaptureAttemptsQueryValidator()
    {
        RuleFor(query => query.OrderId).NotEmpty();
    }
}
