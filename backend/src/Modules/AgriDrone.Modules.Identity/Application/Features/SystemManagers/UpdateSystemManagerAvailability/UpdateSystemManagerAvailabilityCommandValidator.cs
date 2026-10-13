using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class UpdateSystemManagerAvailabilityCommandValidator
    : AbstractValidator<UpdateSystemManagerAvailabilityCommand>
{
    public UpdateSystemManagerAvailabilityCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Availability).IsInEnum();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
    }
}
