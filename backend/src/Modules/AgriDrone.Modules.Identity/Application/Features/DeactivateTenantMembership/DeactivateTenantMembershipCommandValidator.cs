using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.DeactivateTenantMembership
{
    internal sealed class DeactivateTenantMembershipCommandValidator : AbstractValidator<DeactivateTenantMembershipCommand>
    {
        public DeactivateTenantMembershipCommandValidator()
        {
            RuleFor(x => x.tenantId)
                .NotEmpty().WithMessage("TenantId is required.");
        }
    }
}
