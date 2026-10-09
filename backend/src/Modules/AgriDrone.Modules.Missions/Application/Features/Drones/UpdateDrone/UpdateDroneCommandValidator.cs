using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.UpdateDrone;

internal sealed class UpdateDroneCommandValidator : AbstractValidator<UpdateDroneCommand>
{
    public UpdateDroneCommandValidator()
    {
        RuleFor(command => command.DroneId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Model).MaximumLength(100);
        RuleFor(command => command.Manufacturer).MaximumLength(150);
        RuleFor(command => command.SerialNumber).MaximumLength(100);
        RuleFor(command => command.RegistrationNumber).MaximumLength(100);
        RuleFor(command => command.WeightKg).GreaterThan(0).When(command => command.WeightKg.HasValue);
        RuleFor(command => command.Specifications)
            .Must(specifications => !specifications.HasValue ||
                specifications.Value.ValueKind is System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined ||
                AgriDrone.Modules.Missions.Domain.Drones.DroneCapabilityPolicy.IsValid(specifications.Value))
            .WithMessage("Specifications must use supported capabilities.");
        RuleFor(command => command.RegistrationExpiryDate)
            .GreaterThanOrEqualTo(command => command.RegistrationDate)
            .When(command => command.RegistrationDate.HasValue && command.RegistrationExpiryDate.HasValue);
    }
}
