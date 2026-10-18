using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.ReviewExtraFlight;

public sealed class ReviewExtraFlightCommandValidator
    : AbstractValidator<ReviewExtraFlightCommand>
{
    public ReviewExtraFlightCommandValidator()
    {
        RuleFor(x => x.SurveyOrderId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
    }
}
