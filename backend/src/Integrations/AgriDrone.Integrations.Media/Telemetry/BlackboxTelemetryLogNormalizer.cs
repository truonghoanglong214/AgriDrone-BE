using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
using AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedKernel.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.Media.Telemetry;

internal sealed class BlackboxTelemetryLogNormalizer(
    IOptions<BlackboxDecoderOptions> options,
    ILogger<BlackboxTelemetryLogNormalizer> logger)
    : ITelemetryLogNormalizer, IDisposable
{
    private static readonly Action<ILogger, Exception?>
        LogNormalizationFailed =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(1, "BlackboxNormalizationFailed"),
                "Blackbox telemetry normalization failed.");

    private static readonly Action<ILogger, string, Exception?>
        LogWorkspaceCleanupFailed =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(2, "BlackboxWorkspaceCleanupFailed"),
                "Could not clean telemetry workspace {Workspace}.");

    private static readonly Action<ILogger, int, Exception?>
        LogDecoderExitFailed =
            LoggerMessage.Define<int>(
                LogLevel.Warning,
                new EventId(3, "BlackboxDecoderExitFailed"),
                "Blackbox decoder exited with code {ExitCode}.");

    private static readonly CultureInfo Invariant =
        CultureInfo.InvariantCulture;

    private readonly BlackboxDecoderOptions _options = options.Value;
    private readonly SemaphoreSlim _decoderGate = new(1, 1);

    public async Task<Result<NormalizedTelemetryLog>> NormalizeAsync(
        Stream content,
        string sourceFileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName) ||
            sourceFileName.Length > 255 ||
            sourceFileName.Any(char.IsControl))
        {
            return Invalid("Source file name is invalid.");
        }

        var extension = Path.GetExtension(sourceFileName);

        if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".bbl", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("Only INAV Blackbox TXT/BBL files are supported.");
        }

        if (!content.CanRead)
            return Invalid("The uploaded file cannot be read.");

        await _decoderGate.WaitAsync(cancellationToken);

        string? workspace = null;

        try
        {
            workspace = Directory.CreateTempSubdirectory(
                "agridrone-blackbox-").FullName;

            // Never use the uploaded file name as an executable argument path.
            var inputPath = Path.Combine(workspace, "flight.TXT");

            await CopyWithLimitAsync(
                content,
                inputPath,
                cancellationToken);

            var bytes = await File.ReadAllBytesAsync(
                inputPath,
                cancellationToken);

            if (bytes.Length == 0)
                return Invalid("The uploaded log is empty.");

            var signatureLength = Math.Min(bytes.Length, 256);

            var signature = Encoding.ASCII.GetString(
                bytes,
                0,
                signatureLength);

            if (!signature.StartsWith(
                    "H Product:Blackbox flight data recorder",
                    StringComparison.Ordinal))
            {
                return Invalid(
                    "The file is not a supported Blackbox binary log.");
            }

            // Latin1 preserves the byte values while inspecting ASCII headers.
            // The actual flight frames are decoded only by blackbox_decode.
            var headerText = Encoding.Latin1.GetString(bytes);

            var firmwareHeaders = Regex.Matches(
                headerText,
                @"(?m)^H Firmware revision:([^\r\n]+)");

            if (firmwareHeaders.Count == 0 ||
                firmwareHeaders.Cast<Match>().Any(match =>
                    !match.Groups[1].Value.StartsWith(
                        "INAV ",
                        StringComparison.Ordinal)))
            {
                return Invalid(
                    "This adapter supports INAV logs only.");
            }

            var checksum = Convert.ToHexString(
                    SHA256.HashData(bytes))
                .ToLowerInvariant();

            var diagnostics = await DecodeAsync(
                workspace,
                inputPath,
                cancellationToken);

            var warnings = new List<string>
            {
                "UTC timestamps are derived from the log clock; " +
                "verify them against the actual mission.",

                "GPS altitude reference is unverified and is marked Unknown.",

                "GPS ground course is not aircraft heading; " +
                "headingDeg is left null."
            };

            if (diagnostics.Contains(
                    "failed to decode",
                    StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    "The decoder reported failed frames. " +
                    "Decoded points require review; missing samples " +
                    "have not been reconstructed.");
            }

            if (diagnostics.Contains(
                    "[diagnostics truncated]",
                    StringComparison.Ordinal))
            {
                warnings.Add(
                    "Decoder diagnostics exceeded the retained limit; " +
                    "review this conversion before importing.");
            }

            var gpsFiles = Directory
                .EnumerateFiles(workspace, "flight.*.gps.csv")
                .Select(path => new
                {
                    Path = path,
                    Index = ReadSegmentIndex(path)
                })
                .OrderBy(item => item.Index)
                .ToArray();

            if (gpsFiles.Length == 0)
                return Invalid("The decoder did not produce GPS data.");

            var segments = new List<NormalizedTelemetrySegment>();

            foreach (var file in gpsFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                segments.Add(ParseGpsCsv(
                    file.Path,
                    file.Index,
                    cancellationToken));
            }

            if (segments.All(segment => segment.Points.Count < 2))
            {
                return Invalid(
                    "No segment contains at least two valid GPS points.");
            }

            return Result.Success(
                new NormalizedTelemetryLog(
                    sourceFileName,
                    checksum,
                    segments,
                    warnings));
        }
        catch (FormatException exception)
        {
            return Invalid(exception.Message);
        }
        catch (TimeoutException)
        {
            return Result.Failure<NormalizedTelemetryLog>(
                AppError.Conflict(
                    "Telemetry.DecoderTimeout",
                    "Blackbox decoding exceeded the configured timeout."));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            System.ComponentModel.Win32Exception)
        {
            LogNormalizationFailed(logger, exception);

            return Result.Failure<NormalizedTelemetryLog>(
                AppError.Conflict(
                    "Telemetry.DecoderUnavailable",
                    "Telemetry decoding failed. Check the decoder " +
                    "configuration and server logs."));
        }
        finally
        {
            if (workspace is not null)
            {
                try
                {
                    // Only delete the directory generated by this request.
                    Directory.Delete(workspace, recursive: true);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    LogWorkspaceCleanupFailed(
                        logger,
                        workspace,
                        exception);
                }
            }

            _decoderGate.Release();
        }
    }

    private async Task CopyWithLimitAsync(
        Stream source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var target = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(
                buffer.AsMemory(),
                cancellationToken);

            if (read == 0)
                break;

            total += read;

            if (total > _options.MaximumFileBytes)
            {
                throw new FormatException(
                    "The log exceeds the configured upload size limit.");
            }

            await target.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken);
        }
    }

    private async Task<string> DecodeAsync(
        string workspace,
        string inputPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            WorkingDirectory = workspace,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("--datetime");
        startInfo.ArgumentList.Add("--unit-height");
        startInfo.ArgumentList.Add("m");
        startInfo.ArgumentList.Add("--unit-gps-speed");
        startInfo.ArgumentList.Add("mps");
        startInfo.ArgumentList.Add(inputPath);

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
            throw new InvalidOperationException("Decoder could not start.");

        // Drain both streams concurrently to prevent process pipe deadlocks.
        var stdoutTask = ReadBoundedDiagnosticsAsync(
            process.StandardOutput);

        var stderrTask = ReadBoundedDiagnosticsAsync(
            process.StandardError);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);

        timeout.CancelAfter(
            TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between HasExited and Kill.
            }

            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdoutTask, stderrTask);

            cancellationToken.ThrowIfCancellationRequested();

            throw new TimeoutException();
        }

        var outputs = await Task.WhenAll(stdoutTask, stderrTask);
        var diagnostics = string.Join(Environment.NewLine, outputs);

        if (process.ExitCode != 0)
        {
            LogDecoderExitFailed(
                logger,
                process.ExitCode,
                null);

            throw new FormatException(
                "The decoder could not read this log. " +
                "Check the file and firmware compatibility.");
        }

        return diagnostics;
    }

    private static async Task<string> ReadBoundedDiagnosticsAsync(
        StreamReader reader)
    {
        const int maximumCharacters = 128 * 1024;

        var output = new StringBuilder();
        var buffer = new char[4096];
        var truncated = false;

        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory());

            if (count == 0)
                break;

            var available = maximumCharacters - output.Length;
            var retained = Math.Min(available, count);

            if (retained > 0)
                output.Append(buffer, 0, retained);

            if (retained < count)
                truncated = true;
        }

        if (truncated)
            output.AppendLine("[diagnostics truncated]");

        return output.ToString();
    }

    private static int ReadSegmentIndex(string path)
    {
        var match = Regex.Match(
            Path.GetFileName(path),
            @"^flight\.(\d+)\.gps\.csv$",
            RegexOptions.CultureInvariant);

        if (!match.Success ||
            !int.TryParse(
                match.Groups[1].Value,
                NumberStyles.None,
                Invariant,
                out var index))
        {
            throw new FormatException(
                "Unexpected GPS output file name.");
        }

        return index;
    }

    private static NormalizedTelemetrySegment ParseGpsCsv(
        string path,
        int segmentIndex,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(path);

        var header = reader.ReadLine()
            ?? throw new FormatException("GPS CSV header is missing.");

        // This parser targets the decoder's numeric GPS CSV output,
        // not arbitrary user-provided CSV files.
        var columns = header
            .Split(',')
            .Select((name, index) => new
            {
                Name = name.Trim(),
                Index = index
            })
            .ToDictionary(
                item => item.Name,
                item => item.Index,
                StringComparer.Ordinal);

        string[] required =
        [
            "dateTime",
            "GPS_fixType",
            "GPS_coord[0]",
            "GPS_coord[1]",
            "GPS_altitude",
            "GPS_speed (m/s)",
            "GPS_eph"
        ];

        if (required.Any(name => !columns.ContainsKey(name)))
        {
            throw new FormatException(
                "The decoder GPS schema is incompatible.");
        }

        var points = new List<ImportTelemetryPoint>();
        var warnings = new List<string>();

        var sourceCount = 0;
        var rejectedCount = 0;
        DateTimeOffset? previousTime = null;

        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            sourceCount++;

            var fields = line.Split(',');

            if (fields.Length != columns.Count)
            {
                throw new FormatException(
                    $"Malformed GPS row in segment {segmentIndex}.");
            }

            string Field(string name) =>
                fields[columns[name]].Trim();

            decimal Number(string name)
            {
                if (!decimal.TryParse(
                        Field(name),
                        NumberStyles.Float,
                        Invariant,
                        out var number))
                {
                    throw new FormatException(
                        $"Invalid {name} in segment {segmentIndex}.");
                }

                return number;
            }

            // INAV: 0 = no fix, 1 = 2D, 2 = 3D.
            if (Number("GPS_fixType") != 2)
            {
                rejectedCount++;
                continue;
            }

            var rawTime = Field("dateTime");

            if (!rawTime.EndsWith('Z') ||
                !DateTimeOffset.TryParse(
                    rawTime,
                    Invariant,
                    DateTimeStyles.None,
                    out var recordedAt) ||
                recordedAt == default ||
                recordedAt.Offset != TimeSpan.Zero)
            {
                throw new FormatException(
                    $"Invalid UTC time in segment {segmentIndex}.");
            }

            if (previousTime.HasValue &&
                recordedAt <= previousTime.Value)
            {
                throw new FormatException(
                    $"GPS timestamps do not increase in segment {segmentIndex}.");
            }

            var latitude = Number("GPS_coord[0]");
            var longitude = Number("GPS_coord[1]");
            var altitude = Number("GPS_altitude");
            var speed = Number("GPS_speed (m/s)");
            var accuracy = Number("GPS_eph") / 100m;

            if (latitude is < -90m or > 90m ||
                longitude is < -180m or > 180m ||
                altitude is < -999_999.999m or > 999_999.999m ||
                speed is < 0m or > 99_999.999m ||
                accuracy is < 0m or > 99_999.999m)
            {
                throw new FormatException(
                    $"GPS value is outside telemetry limits " +
                    $"in segment {segmentIndex}.");
            }

            points.Add(new ImportTelemetryPoint(
                SequenceNumber: points.Count,
                RecordedAt: recordedAt,
                Longitude: (double)longitude,
                Latitude: (double)latitude,
                AltitudeM: altitude,
                AltitudeReference: AltitudeReference.Unknown,
                HeadingDeg: null,
                SpeedMps: speed,
                HorizontalAccuracyM: accuracy));

            previousTime = recordedAt;
        }

        if (rejectedCount > 0)
        {
            warnings.Add(
                $"{rejectedCount} GPS rows lacked a 3D fix and were excluded. " +
                "Review gaps before constructing a route.");
        }

        if (points.Count < 2)
        {
            warnings.Add(
                "This segment has fewer than two usable GPS points.");
        }

        if (points.Count > 50_000)
        {
            warnings.Add(
                "This segment exceeds the current 50,000-point import limit. " +
                "It has not been automatically downsampled.");
        }

        return new NormalizedTelemetrySegment(
            segmentIndex,
            sourceCount,
            rejectedCount,
            points,
            warnings);
    }

    private static Result<NormalizedTelemetryLog> Invalid(
        string message)
    {
        return Result.Failure<NormalizedTelemetryLog>(
            AppError.Validation(
                "Telemetry.InvalidBlackboxLog",
                message));
    }

    public void Dispose()
    {
        _decoderGate.Dispose();
        GC.SuppressFinalize(this);
    }
}