using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.GetUserTenants;

internal sealed class GetUserTenantsQueryValidator
    : AbstractValidator<GetUserTenantsQuery>
{
    public GetUserTenantsQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
