using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ScheduleMission;

internal sealed class ScheduleMissionCommandValidator
    : AbstractValidator<ScheduleMissionCommand>
{
    public ScheduleMissionCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.MissionId)
            .NotEmpty();

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0u);

        RuleFor(command => command.ScheduledAt)
            .NotEqual(default(DateTimeOffset))
            .Must(timestamp =>
                timestamp.Offset == TimeSpan.Zero)
            .WithMessage(
                "ScheduledAt must be a UTC timestamp.");

        RuleFor(command => command.ScheduledEndAt)
            .NotEqual(default(DateTimeOffset))
            .Must(timestamp =>
                timestamp.Offset == TimeSpan.Zero)
            .WithMessage(
                "ScheduledEndAt must be a UTC timestamp.");

        RuleFor(command => command)
            .Must(command =>
                command.ScheduledEndAt >
                command.ScheduledAt)
            .WithMessage(
                "ScheduledEndAt must be later than ScheduledAt.");
    }
}
