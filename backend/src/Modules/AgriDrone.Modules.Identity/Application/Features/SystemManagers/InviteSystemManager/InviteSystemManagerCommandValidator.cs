using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class InviteSystemManagerCommandValidator
    : AbstractValidator<InviteSystemManagerCommand>
{
    public InviteSystemManagerCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
    }
}
