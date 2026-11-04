using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

internal sealed class PrepareMissionSetCommandValidator
    : AbstractValidator<PrepareMissionSetCommand>
{
    public PrepareMissionSetCommandValidator()
    {
        RuleFor(command => command.SurveyOrderId).NotEmpty();
        RuleFor(command => command.DroneId).NotEmpty();
        RuleFor(command => command.IdempotencyKey).NotEmpty();
    }
}
