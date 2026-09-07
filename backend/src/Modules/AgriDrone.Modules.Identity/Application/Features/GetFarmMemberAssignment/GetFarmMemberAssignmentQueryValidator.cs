using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;

internal sealed class GetFarmMemberAssignmentQueryValidator
    : AbstractValidator<GetFarmMemberAssignmentQuery>
{
    public GetFarmMemberAssignmentQueryValidator()
    {
        RuleFor(query => query.FarmId).NotEmpty();
        RuleFor(query => query.UserId).NotEmpty();
    }
}
