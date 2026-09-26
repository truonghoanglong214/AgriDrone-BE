using AgriDrone.Modules.Identity.Domain.SystemManagers;
using FluentValidation;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

internal sealed class UpdateSystemManagerQualificationCommandValidator
    : AbstractValidator<UpdateSystemManagerQualificationCommand>
{
    public UpdateSystemManagerQualificationCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Reason).ValidReason();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0);
        RuleFor(command => command.ExpiresAt)
            .NotNull()
            .When(command =>
                command.Status == FlightQualificationStatus.Qualified);
    }
}
