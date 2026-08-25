using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenant
{
    internal sealed class GetTenantQueryValidator : AbstractValidator<GetTenantsQuery>
    {
        public GetTenantQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                 .InclusiveBetween(1, int.MaxValue).WithMessage("Page number must be a positive integer.");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        }
    }
}
