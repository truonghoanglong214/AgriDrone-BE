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

        RuleFor(command => command.Segments)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(segments => segments.Count > 0)
            .WithMessage("At least one telemetry segment must be selected.")
            .Must(HaveUniqueSegmentIndexes)
            .WithMessage("Selected segment indexes must be unique and non-negative.")
            .Must(HaveValidPointCount)
            .WithMessage(
                "The selected segments must contain between 2 and " +
                $"{MaximumPointCount} total points.")
            .Must(HaveUniqueRecordedAt)
            .WithMessage(
                "Telemetry point timestamps must be unique across selected segments.");

        RuleForEach(command => command.Segments)
            .ChildRules(point =>
            {
                point.RuleFor(segment => segment.SegmentIndex)
                    .GreaterThanOrEqualTo(0);

                point.RuleFor(segment => segment.Points)
                    .NotNull()
                    .Must(points => points.Count > 0)
                    .WithMessage("A selected segment must contain at least one point.");

                point.RuleForEach(segment => segment.Points)
                    .ChildRules(telemetryPoint =>
                    {
                        telemetryPoint.RuleFor(value => value.RecordedAt)
                            .Must(IsUtc)
                            .WithMessage("RecordedAt must be a non-default UTC timestamp.");

                        telemetryPoint.RuleFor(value => value.Longitude)
                            .Must(longitude =>
                                double.IsFinite(longitude) &&
                                longitude is >= -180 and <= 180)
                            .WithMessage("Longitude must be finite and in the range [-180, 180].");

                        telemetryPoint.RuleFor(value => value.Latitude)
                            .Must(latitude =>
                                double.IsFinite(latitude) &&
                                latitude is >= -90 and <= 90)
                            .WithMessage("Latitude must be finite and in the range [-90, 90].");

                        telemetryPoint.RuleFor(value => value.AltitudeM)
                            .Must(altitude =>
                                !altitude.HasValue ||
                                altitude.Value is >= -999_999.999m and <= 999_999.999m)
                            .WithMessage("Altitude is outside the supported range.");

                        telemetryPoint.RuleFor(value => value.AltitudeReference)
                            .Must(reference => !reference.HasValue || Enum.IsDefined(reference.Value))
                            .WithMessage("AltitudeReference is invalid.");

                        telemetryPoint.RuleFor(value => value.HeadingDeg)
                            .Must(heading => !heading.HasValue || heading.Value is >= 0 and < 360)
                            .WithMessage("HeadingDeg must be in the range [0, 360).");

                        telemetryPoint.RuleFor(value => value.SpeedMps)
                            .Must(speed => !speed.HasValue || speed.Value is >= 0 and <= 99_999.999m)
                            .WithMessage("SpeedMps is outside the supported range.");

                        telemetryPoint.RuleFor(value => value.HorizontalAccuracyM)
                            .Must(accuracy => !accuracy.HasValue || accuracy.Value is >= 0 and <= 99_999.999m)
                            .WithMessage("HorizontalAccuracyM is outside the supported range.");
                    });
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

    private static bool HaveUniqueSegmentIndexes(
        IReadOnlyList<ImportTelemetrySegment> segments)
    {
        return segments.All(segment => segment.SegmentIndex >= 0) &&
               segments.Select(segment => segment.SegmentIndex).Distinct().Count() == segments.Count;
    }

    private static bool HaveValidPointCount(
        IReadOnlyList<ImportTelemetrySegment> segments)
    {
        var pointCount = segments.Sum(segment => segment.Points.Count);
        return pointCount is >= 2 and <= MaximumPointCount;
    }

    private static bool HaveUniqueRecordedAt(
        IReadOnlyList<ImportTelemetrySegment> segments)
    {
        var timestamps = segments
            .SelectMany(segment => segment.Points)
            .Select(point => point.RecordedAt)
            .ToArray();

        return timestamps.Distinct().Count() == timestamps.Length;
    }
}
