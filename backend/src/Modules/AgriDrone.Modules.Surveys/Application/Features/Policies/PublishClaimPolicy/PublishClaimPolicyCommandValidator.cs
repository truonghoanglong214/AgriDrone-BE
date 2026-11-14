using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishClaimPolicy;

internal sealed class PublishClaimPolicyCommandValidator
    : AbstractValidator<PublishClaimPolicyCommand>
{
    public PublishClaimPolicyCommandValidator()
    {
        RuleFor(x => x.CalendarWindowDays).GreaterThanOrEqualTo(1);
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ExpectedLatestVersion)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExpectedLatestVersion.HasValue);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EvidenceReference).MaximumLength(500);
    }
}
