using AgriDrone.Modules.Surveys.Domain;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitExistingFarmSurveyRequest;

internal sealed class SubmitExistingFarmSurveyRequestCommandValidator
    : AbstractValidator<SubmitExistingFarmSurveyRequestCommand>
{
    public SubmitExistingFarmSurveyRequestCommandValidator()
    {
        RuleFor(command => command.FarmId).NotEmpty();
        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .Must(key =>
                key is not null &&
                key.Trim().Length <= RequestIdempotency.MaximumKeyLength)
            .WithMessage(
                $"Idempotency key cannot exceed {RequestIdempotency.MaximumKeyLength} characters after normalization.");
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.EstimatedPoleCount)
            .GreaterThan(0)
            .When(command => command.EstimatedPoleCount.HasValue);
        RuleFor(command => command.Notes)
            .MaximumLength(4_000)
            .When(command => command.Notes is not null);
        RuleFor(command => command.PreferredStartAt)
            .Must(value => !value.HasValue || IsUtc(value.Value))
            .WithMessage(
                "PreferredStartAt must be a non-default UTC timestamp when provided.");
        RuleFor(command => command.PreferredEndAt)
            .Must(value => !value.HasValue || IsUtc(value.Value))
            .WithMessage(
                "PreferredEndAt must be a non-default UTC timestamp when provided.");
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
            .WithMessage(
                "PreferredEndAt must be later than PreferredStartAt.");
    }

    private static bool IsUtc(DateTimeOffset value) =>
        value != default && value.Offset == TimeSpan.Zero;
}
