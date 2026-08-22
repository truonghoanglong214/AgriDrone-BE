using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.DeactivateTenant
{
    internal sealed class DeactivateTenantCommandValidator : AbstractValidator<DeactivateTenantCommand>
    {
        public DeactivateTenantCommandValidator()
        {
            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("TenantId is required.");
        }
    }
}
