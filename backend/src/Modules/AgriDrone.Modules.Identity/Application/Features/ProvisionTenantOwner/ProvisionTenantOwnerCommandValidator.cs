using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.ProvisionTenantOwner;

internal sealed class ProvisionTenantOwnerCommandValidator
    : AbstractValidator<ProvisionTenantOwnerCommand>
{
    public ProvisionTenantOwnerCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required.");

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");
    }
}
