using System.Text.Json;
using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;

internal sealed class CompleteMissionPreflightCommandValidator
    : AbstractValidator<CompleteMissionPreflightCommand>
{
    public CompleteMissionPreflightCommandValidator()
    {
        RuleFor(command => command.FarmId).NotEmpty();
        RuleFor(command => command.MissionId).NotEmpty();
        RuleFor(command => command.OperationId).NotEmpty();
        RuleFor(command => command.ExpectedVersion).GreaterThan(0u);
        RuleFor(command => command.ChecklistDefinitionId).NotEmpty();
        RuleFor(command => command.ChecklistVersion)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(command => command.Answers)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(document =>
                document is not null &&
                document.RootElement.ValueKind == JsonValueKind.Object)
            .WithMessage("Checklist answers must be a JSON object.");
        RuleFor(command => command.Notes).MaximumLength(2000);
        RuleFor(command => command.UnsuitableConditionNotes).MaximumLength(2000);
        RuleFor(command => command.FailsafeNotes).MaximumLength(2000);
        RuleFor(command => command.FlightAuthorizationEvidence).MaximumLength(2000);
        RuleFor(command => command.DeviceCompletedAt)
            .Must(value => !value.HasValue || value.Value.Offset == TimeSpan.Zero)
            .WithMessage("Device completion time must be UTC.");
    }
}
