using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.AcceptTenantInvitation;

internal sealed class AcceptTenantInvitationCommandValidator
    : AbstractValidator<AcceptTenantInvitationCommand>
{
    public AcceptTenantInvitationCommandValidator()
    {
        RuleFor(command => command.Token)
            .NotEmpty()
            .MaximumLength(512);

        When(
            command => command.Password is not null,
            () => RuleFor(command => command.Password!)
                .NotEmpty()
                .MinimumLength(8));

        When(
            command => command.FullName is not null,
            () => RuleFor(command => command.FullName!)
                .NotEmpty()
                .MaximumLength(150));

        When(
            command => command.Phone is not null,
            () => RuleFor(command => command.Phone!)
                .MaximumLength(30));
    }
}
