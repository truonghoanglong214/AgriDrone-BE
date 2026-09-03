using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.RegisterDrone;

internal sealed class RegisterDroneCommandValidator
    : AbstractValidator<RegisterDroneCommand>
{
    public RegisterDroneCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

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