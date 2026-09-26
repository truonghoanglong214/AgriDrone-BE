using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class ActivateSystemManagerProfileCommandValidator
    : AbstractValidator<ActivateSystemManagerProfileCommand>
{
    public ActivateSystemManagerProfileCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
