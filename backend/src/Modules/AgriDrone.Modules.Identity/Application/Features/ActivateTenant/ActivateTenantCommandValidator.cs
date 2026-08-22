using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.ActivateTenant
{
    internal sealed class ActivateTenantCommandValidator : AbstractValidator<ActivateTenantCommand>
    {
        public ActivateTenantCommandValidator()
        {
            RuleFor(x => x.TenantId)
                .NotEmpty().WithMessage("TenantId is required.");
        }
    }
}
