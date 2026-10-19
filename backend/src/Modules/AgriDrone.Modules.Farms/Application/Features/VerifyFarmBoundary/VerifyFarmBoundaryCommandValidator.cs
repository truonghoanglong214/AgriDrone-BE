using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.VerifyFarmBoundary;

public sealed class VerifyFarmBoundaryCommandValidator
    : AbstractValidator<VerifyFarmBoundaryCommand>
{
    public VerifyFarmBoundaryCommandValidator()
    {
        RuleFor(x => x.FarmId)
            .NotEmpty()
            .WithMessage("Farm identifier must not be empty.");

        RuleFor(x => x.Boundary)
            .NotNull()
            .WithMessage("Boundary geometry is required.")
            .Must(b => b != null && b.SRID == 4326 && !b.IsEmpty && b.IsValid && b.Area > 0)
            .WithMessage("Boundary must be a valid, non-empty WGS84 polygon with positive area.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(2000)
            .WithMessage("Verification reason is required and must not exceed 2000 characters.");

        RuleFor(x => x.EvidenceReference)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrEmpty(x.EvidenceReference))
            .WithMessage("Evidence reference must not exceed 1000 characters.");
    }
}
