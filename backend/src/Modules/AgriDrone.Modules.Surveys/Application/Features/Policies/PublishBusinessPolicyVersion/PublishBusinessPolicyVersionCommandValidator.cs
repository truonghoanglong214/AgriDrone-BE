using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishBusinessPolicyVersion;

internal sealed class PublishBusinessPolicyVersionCommandValidator
    : AbstractValidator<PublishBusinessPolicyVersionCommand>
{
    public PublishBusinessPolicyVersionCommandValidator()
    {
        RuleFor(x => x.StreamKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SurveyDepositPolicyId).NotEmpty();
        RuleFor(x => x.ClaimPolicyId).NotEmpty();
        RuleFor(x => x.CancellationPolicyId).NotEmpty();
        RuleFor(x => x.ServiceRevisionIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("Service revision IDs must not contain empty GUIDs.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Service revision IDs must be unique within a bundle.");
        RuleFor(x => x.PricingCurrency)
            .NotEmpty()
            .Length(3)
            .Must(c => string.Equals(c.Trim(), "VND", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only VND pricing currency is supported.");
        RuleFor(x => x.ExpectedLatestVersion)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExpectedLatestVersion.HasValue);
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EvidenceReference).MaximumLength(500);
    }
}
