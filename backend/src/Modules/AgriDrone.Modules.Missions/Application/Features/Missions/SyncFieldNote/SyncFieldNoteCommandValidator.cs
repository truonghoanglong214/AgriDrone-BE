using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.SyncFieldNote;

internal sealed class SyncFieldNoteCommandValidator : AbstractValidator<SyncFieldNoteCommand>
{
    public SyncFieldNoteCommandValidator()
    {
        RuleFor(command => command.FarmId).NotEmpty();
        RuleFor(command => command.MissionId).NotEmpty();
        RuleFor(command => command.OperationId).NotEmpty();
        RuleFor(command => command.Text).NotEmpty().MaximumLength(2000)
            .Must(text => text is not null && !text.Any(char.IsControl));
        RuleFor(command => command.ObservedAt)
            .Must(value => value != default && value.Offset == TimeSpan.Zero)
            .WithMessage("ObservedAt must be a UTC timestamp.");
        RuleFor(command => command.IncidentType)
            .Must(value => value is null or "SIGNAL_LOSS" or "LOW_BATTERY" or
                "INTERRUPTION" or "FLIGHT_FAILURE");
        RuleFor(command => command.IncidentOutcome).MaximumLength(1000);
        RuleFor(command => command.EvidenceReference).MaximumLength(500);
        RuleFor(command => command.RecoveryDecision)
            .Must(value => value is null or "CONTINUE" or "ABORT" or
                "RESCHEDULE_REQUIRED");
        RuleFor(command => command)
            .Must(command => command.IncidentType is null
                ? command.IncidentOutcome is null &&
                  command.RecoveryDecision is null &&
                  command.EvidenceReference is null
                : !string.IsNullOrWhiteSpace(command.IncidentOutcome) &&
                  command.RecoveryDecision is not null &&
                  !string.IsNullOrWhiteSpace(command.EvidenceReference));
    }
}
