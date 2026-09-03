using AgriDrone.Modules.Missions.Domain.Drones;
using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;

internal sealed class ChangeDroneStatusCommandValidator
    : AbstractValidator<ChangeDroneStatusCommand>
{
    public ChangeDroneStatusCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.DroneId)
            .NotEmpty();

        RuleFor(command => command.TargetStatus)
            .Must(status =>
                status is
                    DroneStatus.Available or
                    DroneStatus.Maintenance or
                    DroneStatus.Retired)
            .WithMessage(
                "Only Available, Maintenance and Retired are supported.");

        RuleFor(command => command)
            .Must(command =>
                command.TargetStatus == DroneStatus.Available ||
                !command.NextMaintenanceAt.HasValue)
            .WithMessage(
                "NextMaintenanceAt can only be supplied when completing maintenance.");
    }
}