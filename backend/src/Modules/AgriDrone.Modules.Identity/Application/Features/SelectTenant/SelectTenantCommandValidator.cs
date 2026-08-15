using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SelectTenant;

internal sealed class SelectTenantCommandValidator
    : AbstractValidator<SelectTenantCommand>
{
    public SelectTenantCommandValidator()
    {
        RuleFor(command => command.SelectionToken).NotEmpty();
        RuleFor(command => command.TenantId).NotEmpty();
    }
}
