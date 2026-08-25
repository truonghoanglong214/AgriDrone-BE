using AgriDrone.SharedKernel.Application;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.ActivateTenantMembership
{
    internal sealed class ActivateTenantMembershipCommandValidator : AbstractValidator<ActivateTenantMembershipCommand>
    {
        public ActivateTenantMembershipCommandValidator()
        {
            RuleFor(x => x.MembershipId)
                .NotEmpty().WithMessage("MembershipId is required.");
        }
    }
}
