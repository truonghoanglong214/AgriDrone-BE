using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.TrackSurveyRequestV3;

internal sealed class TrackSurveyRequestV3QueryValidator
    : AbstractValidator<TrackSurveyRequestV3Query>
{
    public TrackSurveyRequestV3QueryValidator()
    {
        RuleFor(query => query.TrackingToken)
            .NotEmpty()
            .MaximumLength(128)
            .Must(token => !string.IsNullOrWhiteSpace(token) && token.Trim().StartsWith("trk_", StringComparison.Ordinal))
            .WithMessage("A valid tracking token starting with 'trk_' is required.");
    }
}
