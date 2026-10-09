using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.RegisterDrone;

internal sealed class RegisterDroneCommandValidator
    : AbstractValidator<RegisterDroneCommand>
{
    public RegisterDroneCommandValidator()
    {
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Model)
            .MaximumLength(100);

        RuleFor(command => command.Manufacturer)
            .MaximumLength(100);

        RuleFor(command => command.SerialNumber)
            .MaximumLength(100);

        RuleFor(command => command.RegistrationNumber)
            .MaximumLength(100);

        RuleFor(command => command.WeightKg)
            .GreaterThan(0)
            .When(command => command.WeightKg.HasValue);

        RuleFor(command => command.Specifications)
            .Must(specifications => !specifications.HasValue ||
                specifications.Value.ValueKind is System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined ||
                AgriDrone.Modules.Missions.Domain.Drones.DroneCapabilityPolicy.IsValid(specifications.Value))
            .WithMessage("Specifications must use supported capabilities.");

        RuleFor(command => command)
            .Must(command =>
                !command.RegistrationDate.HasValue ||
                !command.RegistrationExpiryDate.HasValue ||
                command.RegistrationExpiryDate.Value >=
                command.RegistrationDate.Value)
            .WithMessage(
                "Registration expiry date cannot be earlier than registration date.");
    }
}
