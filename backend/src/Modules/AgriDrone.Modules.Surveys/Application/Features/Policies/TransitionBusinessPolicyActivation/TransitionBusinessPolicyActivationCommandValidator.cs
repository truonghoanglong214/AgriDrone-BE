using FluentValidation;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.TransitionBusinessPolicyActivation;

internal sealed class TransitionBusinessPolicyActivationCommandValidator
    : AbstractValidator<TransitionBusinessPolicyActivationCommand>
{
    public TransitionBusinessPolicyActivationCommandValidator()
    {
        RuleFor(x => x.BusinessPolicyVersionId).NotEmpty();
        RuleFor(x => x.ReasonCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.EvidenceReference).MaximumLength(500);
    }
}
