using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.RevokeFarmMemberAssignment;

internal sealed class RevokeFarmMemberAssignmentCommandValidator
    : AbstractValidator<RevokeFarmMemberAssignmentCommand>
{
    public RevokeFarmMemberAssignmentCommandValidator()
    {
        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0);

        RuleFor(command => command.Reason)
            .MaximumLength(500);
    }
}
