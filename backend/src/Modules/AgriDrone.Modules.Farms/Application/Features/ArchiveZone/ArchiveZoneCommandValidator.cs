using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.ArchiveZone;

internal sealed class ArchiveZoneCommandValidator
    : AbstractValidator<ArchiveZoneCommand>
{
    public ArchiveZoneCommandValidator()
    {
        RuleFor(command => command.FarmId)
            .NotEmpty().WithMessage("Farm id is required.");

        RuleFor(command => command.ZoneId)
            .NotEmpty().WithMessage("Zone id is required.");

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version must be greater than zero.");
    }
}
