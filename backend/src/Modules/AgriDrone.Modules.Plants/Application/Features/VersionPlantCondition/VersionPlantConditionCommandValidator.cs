using FluentValidation;

namespace AgriDrone.Modules.Plants.Application.Features.VersionPlantCondition;

internal sealed class VersionPlantConditionCommandValidator
    : AbstractValidator<VersionPlantConditionCommand>
{
    public VersionPlantConditionCommandValidator()
    {
        RuleFor(command => command.ConditionId)
            .NotEmpty()
            .WithMessage("Plant condition id is required.");

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.ScientificName)
            .MaximumLength(150);

        RuleFor(command => command.Description)
            .MaximumLength(2000);

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version must be greater than zero.");
    }
}
