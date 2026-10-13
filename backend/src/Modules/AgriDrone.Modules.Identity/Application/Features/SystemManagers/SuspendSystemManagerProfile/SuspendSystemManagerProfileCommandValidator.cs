using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class SuspendSystemManagerProfileCommandValidator
    : AbstractValidator<SuspendSystemManagerProfileCommand>
{
    public SuspendSystemManagerProfileCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
