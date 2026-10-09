using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

internal sealed class PrepareMissionSetCommandValidator
    : AbstractValidator<PrepareMissionSetCommand>
{
    public PrepareMissionSetCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.DroneId).NotEmpty();
        RuleFor(command => command.OperationId).NotEmpty();
        RuleFor(command => command.ServiceWindow).NotNull();
        RuleFor(command => command.ServiceWindow)
            .Must(IsValidWindow)
            .When(command => command.ServiceWindow is not null)
            .WithMessage("Service Mission schedule window is invalid.");
        RuleFor(command => command.BaselineWindow)
            .Must(window => window is null || IsValidWindow(window))
            .WithMessage("Baseline Mission schedule window is invalid.");
    }

    private static bool IsValidWindow(MissionScheduleWindow window) =>
        window.StartAt != default &&
        window.EndAt != default &&
        window.StartAt.Offset == TimeSpan.Zero &&
        window.EndAt.Offset == TimeSpan.Zero &&
        window.EndAt > window.StartAt;
}
