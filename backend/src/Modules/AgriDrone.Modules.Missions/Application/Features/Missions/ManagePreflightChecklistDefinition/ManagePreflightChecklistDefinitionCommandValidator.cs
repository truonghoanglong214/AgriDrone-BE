using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ManagePreflightChecklistDefinition;

internal sealed class ManagePreflightChecklistDefinitionCommandValidator
    : AbstractValidator<ManagePreflightChecklistDefinitionCommand>
{
    public ManagePreflightChecklistDefinitionCommandValidator()
    {
        RuleFor(command => command.Items).NotNull()
            .Must(items => items is not null && items.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            .WithMessage("Checklist items must be a JSON array.");
    }
}
