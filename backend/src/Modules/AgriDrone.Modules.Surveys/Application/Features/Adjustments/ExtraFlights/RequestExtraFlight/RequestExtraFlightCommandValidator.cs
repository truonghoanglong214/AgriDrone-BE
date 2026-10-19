using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.RequestExtraFlight;

public sealed class RequestExtraFlightCommandValidator
    : AbstractValidator<RequestExtraFlightCommand>
{
    public RequestExtraFlightCommandValidator()
    {
        RuleFor(x => x.SurveyOrderId).NotEmpty();
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.EvidenceReference).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.CostImpact).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
