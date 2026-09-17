using FluentValidation;

namespace AgriDrone.Modules.Harvests.Application.Features.RetireHarvestQualityGrade;

internal sealed class RetireHarvestQualityGradeCommandValidator
    : AbstractValidator<RetireHarvestQualityGradeCommand>
{
    public RetireHarvestQualityGradeCommandValidator()
    {
        RuleFor(command => command.GradeId)
            .NotEmpty()
            .WithMessage("Harvest quality grade id is required.");

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version must be greater than zero.");
    }
}
