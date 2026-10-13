using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class AssignPrimaryFarmManagerCommandValidator
    : AbstractValidator<AssignPrimaryFarmManagerCommand>
{
    public AssignPrimaryFarmManagerCommandValidator()
    {
        RuleFor(command => command.FarmId).NotEmpty();
        RuleFor(command => command.SystemManagerProfileId).NotEmpty();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedCurrentAssignmentVersion)
            .GreaterThan(0)
            .When(command => command.ExpectedCurrentAssignmentVersion.HasValue);
    }
}
