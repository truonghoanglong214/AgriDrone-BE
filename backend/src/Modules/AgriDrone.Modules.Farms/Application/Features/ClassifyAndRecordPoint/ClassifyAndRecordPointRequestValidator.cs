using FluentValidation;

namespace AgriDrone.Modules.Farms.Application.Features.ClassifyAndRecordPoint;

public sealed class ClassifyAndRecordPointRequestValidator
    : AbstractValidator<ClassifyAndRecordPointRequest>
{
    public ClassifyAndRecordPointRequestValidator()
    {
        RuleFor(x => x.Command)
            .NotNull()
            .WithMessage("Command is required.");

        When(x => x.Command is not null, () =>
        {
            RuleFor(x => x.Command.FarmId)
                .NotEmpty()
                .WithMessage("Farm identifier is required.");

            RuleFor(x => x.Command.Point)
                .NotNull()
                .WithMessage("Point geometry is required.")
                .Must(p => p != null && p.SRID == 4326 && !p.IsEmpty &&
                           double.IsFinite(p.X) && double.IsFinite(p.Y) &&
                           p.X >= -180 && p.X <= 180 &&
                           p.Y >= -90 && p.Y <= 90)
                .WithMessage("Point must be a valid, finite WGS84 coordinate.");

            RuleFor(x => x.Command.SourceReferenceId)
                .NotEmpty()
                .MaximumLength(200)
                .WithMessage("Source reference identifier is required and must not exceed 200 characters.");

            RuleFor(x => x.Command.NearBoundaryThresholdMeters)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Near boundary threshold meters must be greater than or equal to zero.");
        });
    }
}
