using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;

internal sealed class GetFarmMembersQueryValidator : AbstractValidator<GetFarmMembersQuery>
{
    public GetFarmMembersQueryValidator()
    {
        RuleFor(query => query.FarmId)
            .NotEmpty();

        RuleFor(query => query.Role)
            .IsInEnum()
            .When(query => query.Role.HasValue);

        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);

        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
