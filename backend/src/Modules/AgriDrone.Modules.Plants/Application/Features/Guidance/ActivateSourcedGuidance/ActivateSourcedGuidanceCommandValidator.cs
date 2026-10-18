using FluentValidation;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.ActivateSourcedGuidance;

internal sealed class ActivateSourcedGuidanceCommandValidator
    : AbstractValidator<ActivateSourcedGuidanceCommand>
{
    public ActivateSourcedGuidanceCommandValidator() =>
        RuleFor(command => command.GuidanceId).NotEmpty();
}
