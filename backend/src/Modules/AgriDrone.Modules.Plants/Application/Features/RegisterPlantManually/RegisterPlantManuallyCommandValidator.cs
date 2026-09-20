using FluentValidation;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Application.Features.RegisterPlantManually;

internal sealed class RegisterPlantManuallyCommandValidator
    : AbstractValidator<RegisterPlantManuallyCommand>
{
    public RegisterPlantManuallyCommandValidator()
    {
        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.ZoneId)
            .NotEmpty();

        RuleFor(command => command.PlantCode)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(command => command.Location)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(IsValidLocation)
            .WithMessage(
                "Location must be a finite WGS84 point with valid longitude and latitude.");

        RuleFor(command => command.MapVersionId)
            .Must(mapVersionId =>
                !mapVersionId.HasValue || mapVersionId.Value != Guid.Empty)
            .WithMessage(
                "Map version id cannot be an empty identifier when provided.");

        RuleFor(command => command)
            .Must(HasCompleteGridPosition)
            .WithMessage(
                "Map version, row index, and column index must either all be provided or all be omitted.");

        RuleFor(command => command.RowIndex)
            .GreaterThanOrEqualTo(1)
            .When(command => command.RowIndex.HasValue);

        RuleFor(command => command.ColumnIndex)
            .GreaterThanOrEqualTo(1)
            .When(command => command.ColumnIndex.HasValue);

        RuleFor(command => command.LocationAccuracyM)
            .InclusiveBetween(0m, 99_999.999m)
            .When(command => command.LocationAccuracyM.HasValue);

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(1000);
    }

    private static bool HasCompleteGridPosition(
        RegisterPlantManuallyCommand command)
    {
        var hasMapVersion = command.MapVersionId.HasValue;
        return hasMapVersion == command.RowIndex.HasValue &&
               hasMapVersion == command.ColumnIndex.HasValue;
    }

    private static bool IsValidLocation(Point? location) =>
        location is not null &&
        !location.IsEmpty &&
        location.SRID == 4326 &&
        double.IsFinite(location.X) &&
        double.IsFinite(location.Y) &&
        location.X is >= -180 and <= 180 &&
        location.Y is >= -90 and <= 90;
}
