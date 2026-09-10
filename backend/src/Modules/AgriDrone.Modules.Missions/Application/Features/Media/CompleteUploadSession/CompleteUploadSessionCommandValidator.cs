using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CompleteUploadSession;

internal sealed class CompleteUploadSessionCommandValidator
    : AbstractValidator<CompleteUploadSessionCommand>
{
    public CompleteUploadSessionCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.MissionId)
            .NotEmpty();

        RuleFor(command => command.UploadSessionId)
            .NotEmpty();
    }
}