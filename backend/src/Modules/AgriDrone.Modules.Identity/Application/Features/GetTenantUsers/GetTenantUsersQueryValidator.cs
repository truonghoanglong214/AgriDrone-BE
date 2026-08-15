using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenantUsers
{
    internal sealed class GetTenantUsersQueryValidator : AbstractValidator<GetTenantUsersQuery>
    {
        public GetTenantUsersQueryValidator()
        {
            RuleFor(query => query.PageNumber).GreaterThan(0);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        }
    }
}
