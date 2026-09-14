using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.GetMyFarmAssignments;

internal sealed class GetMyFarmAssignmentsQueryValidator
    : AbstractValidator<GetMyFarmAssignmentsQuery>
{
    public GetMyFarmAssignmentsQueryValidator()
    {
        RuleFor(query => query.Role)
            .IsInEnum()
            .When(query => query.Role.HasValue);

        RuleFor(query => query.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);
    }
}
