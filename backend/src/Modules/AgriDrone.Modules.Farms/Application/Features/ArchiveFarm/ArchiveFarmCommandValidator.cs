using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.ArchiveFarm;

internal sealed class ArchiveFarmCommandValidator
    : AbstractValidator<ArchiveFarmCommand>
{
    public ArchiveFarmCommandValidator()
    {
        RuleFor(command => command.FarmId)
            .NotEmpty().WithMessage("Farm id is required.");

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version must be greater than zero.");

        RuleFor(command => command.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("Archive reason is required.")
            .MaximumLength(500)
            .WithMessage("Archive reason must not exceed 500 characters.");
    }
}
