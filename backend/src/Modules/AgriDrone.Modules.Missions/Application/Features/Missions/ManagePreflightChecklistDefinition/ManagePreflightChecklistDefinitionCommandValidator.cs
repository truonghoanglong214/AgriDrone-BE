using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ManagePreflightChecklistDefinition;

internal sealed class ManagePreflightChecklistDefinitionCommandValidator
    : AbstractValidator<ManagePreflightChecklistDefinitionCommand>
{
    public ManagePreflightChecklistDefinitionCommandValidator()
    {
        RuleFor(command => command)
            .Must(command => command.Items is not null ||
                command.ItemsToAdd is not null ||
                command.KeysToRemove is { Count: > 0 })
            .WithMessage("Provide checklist items or at least one condition to add or remove.");
    }
}
