using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitTenantOwnerSurveyRequestV3;

internal sealed class SubmitTenantOwnerSurveyRequestV3CommandValidator
    : AbstractValidator<SubmitTenantOwnerSurveyRequestV3Command>
{
    public SubmitTenantOwnerSurveyRequestV3CommandValidator()
    {
        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(command => command.SurveyServiceId).NotEmpty();

        When(command => !command.FarmId.HasValue, () =>
        {
            RuleFor(command => command.FarmName)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(command => command.FarmAddress)
                .NotEmpty()
                .MaximumLength(2_000);

            RuleFor(command => command.ApproximateAreaHa)
                .NotNull()
                .GreaterThan(0m)
                .LessThan(100_000_000m)
                .PrecisionScale(12, 4, ignoreTrailingZeros: true);

            RuleFor(command => command.Longitude)
                .NotNull()
                .Must(value => value.HasValue && double.IsFinite(value.Value))
                .InclusiveBetween(-180d, 180d);

            RuleFor(command => command.Latitude)
                .NotNull()
                .Must(value => value.HasValue && double.IsFinite(value.Value))
                .InclusiveBetween(-90d, 90d);
        });

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
