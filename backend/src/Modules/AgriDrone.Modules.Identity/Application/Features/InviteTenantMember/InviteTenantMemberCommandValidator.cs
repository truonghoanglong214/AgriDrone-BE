using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantMember;

internal sealed class InviteTenantMemberCommandValidator
    : AbstractValidator<InviteTenantMemberCommand>
{
    public InviteTenantMemberCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
    }
}
