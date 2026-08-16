using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantAdmin;

internal sealed class InviteTenantAdminCommandValidator
    : AbstractValidator<InviteTenantAdminCommand>
{
    public InviteTenantAdminCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
    }
}
