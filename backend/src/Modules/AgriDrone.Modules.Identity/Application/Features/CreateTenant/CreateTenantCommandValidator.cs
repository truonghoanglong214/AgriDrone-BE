using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.CreateTenant
{
    internal sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
    {
        public CreateTenantCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Tenant name is required.")
                .MaximumLength(150).WithMessage("Tenant name is not more than 150 characters length.");


            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Tenant code is required.")
                .MaximumLength(50).WithMessage("Tenant code is not more than 50 characters length.");
        }
    }
}
