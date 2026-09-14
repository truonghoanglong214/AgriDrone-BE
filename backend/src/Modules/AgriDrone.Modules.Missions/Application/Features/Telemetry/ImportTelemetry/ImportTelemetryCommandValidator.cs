using FluentValidation;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;

internal sealed class ImportTelemetryCommandValidator
    : AbstractValidator<ImportTelemetryCommand>
{
    private const int MaximumPointCount = 50_000;

    public ImportTelemetryCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.MissionId)
            .NotEmpty();

        RuleFor(command => command.OperationId)
            .NotEmpty();

        RuleFor(command => command.ExpectedMissionVersion)
            .GreaterThan(0u);

        RuleFor(command => command.SourceFileName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(255)
            .Must(IsPlainFileName)
            .WithMessage(
                "SourceFileName must be a plain file name " +
                "without path characters.");

        RuleFor(command => command.SourceChecksum)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Length(64)
            .Matches("^[0-9a-fA-F]{64}$")
            .WithMessage(
                "SourceChecksum must contain 64 " +
                "hexadecimal characters.");

        RuleFor(command => command.Points)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(points => points.Count >= 2)
            .WithMessage(
                "At least two telemetry points are required.")
            .Must(points =>
                points.Count <= MaximumPointCount)
            .WithMessage(
                $"A telemetry import cannot exceed " +
                $"{MaximumPointCount} points.")
            .Must(HaveStrictlyIncreasingSequenceAndTime)
            .WithMessage(
                "Sequence numbers and timestamps must " +
                "increase strictly in request order.");

        RuleForEach(command => command.Points)
            .ChildRules(point =>
            {
                point.RuleFor(value =>
                        value.SequenceNumber)
                    .GreaterThanOrEqualTo(0);

                point.RuleFor(value =>
                        value.RecordedAt)
                    .Must(IsUtc)
                    .WithMessage(
                        "RecordedAt must be a non-default " +
                        "UTC timestamp.");

                point.RuleFor(value =>
                        value.Longitude)
                    .Must(longitude =>
                        double.IsFinite(longitude) &&
                        longitude is >= -180 and <= 180)
                    .WithMessage(
                        "Longitude must be finite and in " +
                        "the range [-180, 180].");

                point.RuleFor(value =>
                        value.Latitude)
                    .Must(latitude =>
                        double.IsFinite(latitude) &&
                        latitude is >= -90 and <= 90)
                    .WithMessage(
                        "Latitude must be finite and in " +
                        "the range [-90, 90].");

                point.RuleFor(value =>
                        value.AltitudeM)
                    .Must(altitude =>
                        !altitude.HasValue ||
                        altitude.Value is
                            >= -999_999.999m and
                            <= 999_999.999m)
                    .WithMessage(
                        "Altitude is outside the " +
                        "supported range.");

                point.RuleFor(value =>
                        value.AltitudeReference)
                    .Must(reference =>
                        !reference.HasValue ||
                        Enum.IsDefined(reference.Value))
                    .WithMessage(
                        "AltitudeReference is invalid.");

                point.RuleFor(value =>
                        value.HeadingDeg)
                    .Must(heading =>
                        !heading.HasValue ||
                        heading.Value is >= 0 and < 360)
                    .WithMessage(
                        "HeadingDeg must be in the " +
                        "range [0, 360).");

                point.RuleFor(value =>
                        value.SpeedMps)
                    .Must(speed =>
                        !speed.HasValue ||
                        speed.Value is
                            >= 0 and <= 99_999.999m)
                    .WithMessage(
                        "SpeedMps is outside the " +
                        "supported range.");

                point.RuleFor(value =>
                        value.HorizontalAccuracyM)
                    .Must(accuracy =>
                        !accuracy.HasValue ||
                        accuracy.Value is
                            >= 0 and <= 99_999.999m)
                    .WithMessage(
                        "HorizontalAccuracyM is outside " +
                        "the supported range.");
            });
    }

    private static bool IsPlainFileName(
        string fileName)
    {
        return fileName is not "." and not ".." &&
               !fileName.Contains('/') &&
               !fileName.Contains('\\') &&
               !fileName.Any(char.IsControl);
    }

    private static bool IsUtc(
        DateTimeOffset recordedAt)
    {
        return recordedAt != default &&
               recordedAt.Offset == TimeSpan.Zero;
    }

    private static bool
        HaveStrictlyIncreasingSequenceAndTime(
            IReadOnlyList<ImportTelemetryPoint> points)
    {
        if (points.Count < 2)
        {
            return true;
        }

        for (var index = 1;
             index < points.Count;
             index++)
        {
            if (points[index].SequenceNumber <=
                    points[index - 1].SequenceNumber ||
                points[index].RecordedAt <=
                    points[index - 1].RecordedAt)
            {
                return false;
            }
        }

        return true;
    }
}