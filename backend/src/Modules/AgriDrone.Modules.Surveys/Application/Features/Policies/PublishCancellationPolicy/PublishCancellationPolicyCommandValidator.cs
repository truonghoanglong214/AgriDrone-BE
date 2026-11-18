using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishCancellationPolicy;

internal sealed class PublishCancellationPolicyCommandValidator
    : AbstractValidator<PublishCancellationPolicyCommand>
{
    public PublishCancellationPolicyCommandValidator()
    {
        RuleFor(x => x.AllowedFreeNoShowReschedules).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedLatestVersion)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExpectedLatestVersion.HasValue);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EvidenceReference).MaximumLength(500);
    }
}
