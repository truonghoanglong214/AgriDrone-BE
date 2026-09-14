using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.FinalizeMissionUpload;

internal sealed class FinalizeMissionUploadCommandValidator
    : AbstractValidator<FinalizeMissionUploadCommand>
{
    public FinalizeMissionUploadCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.MissionId)
            .NotEmpty();

        RuleFor(command => command.ExpectedMissionVersion)
            .GreaterThan(0u);
    }
}