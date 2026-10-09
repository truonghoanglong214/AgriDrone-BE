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
    }
}
