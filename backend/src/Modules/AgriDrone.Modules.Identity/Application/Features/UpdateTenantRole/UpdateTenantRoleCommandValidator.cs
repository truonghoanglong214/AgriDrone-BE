using AgriDrone.Modules.Identity.Domain.Tenants;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.UpdateTenantRole
{
    internal sealed class UpdateTenantRoleCommandValidator : AbstractValidator<UpdateTenantRoleCommand>
    {
        public UpdateTenantRoleCommandValidator()
        {
            RuleFor(command => command.UserId)
                .NotEmpty();

            RuleFor(command => command.Role)
                .Must(role => role is
                    TenantMemberRole.Member or
                    TenantMemberRole.TenantAdmin)
                .WithMessage(
                    "Role must be MEMBER or TENANT_ADMIN.");
        }
    }
}
