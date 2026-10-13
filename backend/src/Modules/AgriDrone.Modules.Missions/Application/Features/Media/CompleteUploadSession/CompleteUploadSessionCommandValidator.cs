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

        RuleFor(command => command.CapturedAt)
            .Must(value => !value.HasValue ||
                           (value.Value != default &&
                            value.Value.Offset == TimeSpan.Zero))
            .WithMessage("CapturedAt must be a non-default UTC timestamp.");
    }
}
