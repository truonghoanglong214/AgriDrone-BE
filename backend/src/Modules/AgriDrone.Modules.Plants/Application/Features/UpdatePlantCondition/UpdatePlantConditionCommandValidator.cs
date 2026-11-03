using FluentValidation;

namespace AgriDrone.Modules.Plants.Application.Features.UpdatePlantCondition;

internal sealed class UpdatePlantConditionCommandValidator
    : AbstractValidator<UpdatePlantConditionCommand>
{
    public UpdatePlantConditionCommandValidator()
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
