using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.ConsumeExtraFlight;

public sealed class ConsumeExtraFlightCommandValidator
    : AbstractValidator<ConsumeExtraFlightCommand>
{
    public ConsumeExtraFlightCommandValidator()
    {
        RuleFor(x => x.SurveyOrderId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.ExtraMissionId).NotEmpty();
    }
}
