using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class EndPrimaryFarmManagerAssignmentCommandValidator
    : AbstractValidator<EndPrimaryFarmManagerAssignmentCommand>
{
    public EndPrimaryFarmManagerAssignmentCommandValidator()
    {
        RuleFor(command => command.FarmId).NotEmpty();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
