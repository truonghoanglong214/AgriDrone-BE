using System.Net.Mail;
using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequest;

internal sealed class SubmitNewCustomerSurveyRequestCommandValidator
    : AbstractValidator<SubmitNewCustomerSurveyRequestCommand>
{
    public SubmitNewCustomerSurveyRequestCommandValidator()
    {
        RuleFor(command => command.Idempotency)
            .Must(value => value != default)
            .WithMessage("A valid server-derived caller scope and idempotency key are required.");
        RuleFor(command => command.SurveyServiceId).NotEmpty();
        RuleFor(command => command.ApplicantName)
            .NotEmpty()
            .MaximumLength(150);
        RuleFor(command => command.ApplicantEmail)
            .NotEmpty()
            .MaximumLength(320)
            .Must(IsValidEmail)
            .WithMessage("Applicant email is invalid.");
        RuleFor(command => command.ApplicantPhone)
            .NotEmpty()
            .MaximumLength(30)
            .Must(IsSupportedPhone)
            .WithMessage(
                "Applicant phone must contain 7 to 15 digits and may use a leading '+' and common separators.");
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

    private static bool IsSupportedPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        var digits = 0;
        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (character is >= '0' and <= '9')
            {
                digits++;
                continue;
            }

            if (character == '+' && index == 0 ||
                character is ' ' or '-' or '(' or ')' or '.')
            {
                continue;
            }

            return false;
        }

        return digits is >= 7 and <= 15;
    }

    private static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !MailAddress.TryCreate(value.Trim(), out var address))
        {
            return false;
        }

        return string.Equals(
            address.Address,
            value.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}
