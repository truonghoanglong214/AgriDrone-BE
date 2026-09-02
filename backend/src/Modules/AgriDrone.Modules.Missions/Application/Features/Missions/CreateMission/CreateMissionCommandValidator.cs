using AgriDrone.Modules.Missions.Domain.Missions;
using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.CreateMission;

internal sealed class CreateMissionCommandValidator
    : AbstractValidator<CreateMissionCommand>
{
    public CreateMissionCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.FarmId).NotEmpty();
        RuleFor(command => command.ZoneId).NotEmpty();
        RuleFor(command => command.DroneId).NotEmpty();

        RuleFor(command => command.MissionCode)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(command => command.MissionType)
            .IsInEnum();

        RuleFor(command => command.SourceMapVersionId)
            .NotEmpty()
            .When(command =>
                command.MissionType ==
                MissionType.HealthInspection);

        RuleFor(command => command.SourceMapVersionId)
            .Null()
            .When(command =>
                command.MissionType ==
                MissionType.Mapping);
    }
}