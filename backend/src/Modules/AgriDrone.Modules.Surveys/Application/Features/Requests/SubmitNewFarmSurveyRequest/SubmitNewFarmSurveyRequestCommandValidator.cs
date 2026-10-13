using AgriDrone.Modules.Surveys.Domain;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewFarmSurveyRequest;

internal sealed class SubmitNewFarmSurveyRequestCommandValidator
    : AbstractValidator<SubmitNewFarmSurveyRequestCommand>
{
    public SubmitNewFarmSurveyRequestCommandValidator()
    {
        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .Must(key =>
                key is not null &&
                key.Trim().Length <= RequestIdempotency.MaximumKeyLength)
            .WithMessage(
                $"Idempotency key cannot exceed {RequestIdempotency.MaximumKeyLength} characters after normalization.");
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.FarmName)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(command => command.FarmAddress)
            .NotEmpty()
            .MaximumLength(2_000);
        RuleFor(command => command.ApproximateAreaHa)
            .GreaterThan(0m)
            .LessThan(100_000_000m)
            .PrecisionScale(12, 4, ignoreTrailingZeros: true);
        RuleFor(command => command.Longitude)
            .Must(double.IsFinite)
            .InclusiveBetween(-180d, 180d);
        RuleFor(command => command.Latitude)
            .Must(double.IsFinite)
            .InclusiveBetween(-90d, 90d);
        RuleFor(command => command.EstimatedPoleCount)
            .GreaterThan(0)
            .When(command => command.EstimatedPoleCount.HasValue);
        RuleFor(command => command.Notes)
            .MaximumLength(4_000)
            .When(command => command.Notes is not null);
        RuleFor(command => command.PreferredStartAt)
            .Must(value => !value.HasValue || IsUtc(value.Value))
            .WithMessage("PreferredStartAt must be a non-default UTC timestamp when provided.");
        RuleFor(command => command.PreferredEndAt)
            .Must(value => !value.HasValue || IsUtc(value.Value))
            .WithMessage("PreferredEndAt must be a non-default UTC timestamp when provided.");
        RuleFor(command => command)
            .Must(command =>
                command.PreferredStartAt.HasValue ==
                command.PreferredEndAt.HasValue)
            .WithMessage(
                "PreferredStartAt and PreferredEndAt must both be supplied or both be omitted.");
        RuleFor(command => command)
            .Must(command =>
                !command.PreferredStartAt.HasValue ||
                command.PreferredEndAt > command.PreferredStartAt)
            .WithMessage("PreferredEndAt must be later than PreferredStartAt.");
    }

    private static bool IsUtc(DateTimeOffset value) =>
        value != default && value.Offset == TimeSpan.Zero;
}
