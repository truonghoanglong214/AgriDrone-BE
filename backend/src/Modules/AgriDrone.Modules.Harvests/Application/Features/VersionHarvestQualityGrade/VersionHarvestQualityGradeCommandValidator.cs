using FluentValidation;

namespace AgriDrone.Modules.Harvests.Application.Features.VersionHarvestQualityGrade;

internal sealed class VersionHarvestQualityGradeCommandValidator
    : AbstractValidator<VersionHarvestQualityGradeCommand>
{
    public VersionHarvestQualityGradeCommandValidator()
    {
        RuleFor(command => command.GradeId)
            .NotEmpty()
            .WithMessage("Harvest quality grade id is required.");

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.DisplayOrder)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version must be greater than zero.");
    }
}
