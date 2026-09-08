using FluentValidation;
namespace AgriDrone.Modules.Farms.Application.Features.UpdateZone;

internal sealed class UpdateZoneCommandValidator
    : AbstractValidator<UpdateZoneCommand>
{
    public UpdateZoneCommandValidator()
    {
        RuleFor(command => command.FarmId)
            .NotEmpty().WithMessage("Farm id is required.");

        RuleFor(command => command.ZoneId)
            .NotEmpty().WithMessage("Zone id is required.");

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(command => command.Boundary)
            .Must(boundary => boundary is null ||
                boundary.SRID == 4326 && boundary.IsValid)
            .WithMessage("Boundary must be a valid Polygon with SRID 4326.");

        RuleFor(command => command.AreaHectares)
            .GreaterThanOrEqualTo(0)
            .When(command => command.AreaHectares.HasValue)
            .WithMessage("AreaHectares must be greater than or equal to 0 if provided.");

        RuleFor(command => command.ExpectedVersion)
            .GreaterThan(0)
            .WithMessage("Expected version must be greater than zero.");
    }
}
