using System.Diagnostics;
using System.Globalization;
using System.Text;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class PythonGeotagService(
    IOptions<GeotagOptions> options,
    ILogger<PythonGeotagService> logger) : IGeotagService
{
    private readonly GeotagOptions _options = options.Value;

    public async Task<GeotagResult> ApplyGeotagAsync(
        string imagesDirectory,
        string csvFilePath,
        double timeOffsetSeconds = 0,
        double maxDiffSeconds = 3.0,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(imagesDirectory))
        {
            return new GeotagResult(false, 0, 0, $"Image directory does not exist: {imagesDirectory}");
        }

        if (!File.Exists(csvFilePath))
        {
            return new GeotagResult(false, 0, 0, $"Log CSV file does not exist: {csvFilePath}");
        }

        var scriptPath = ResolveScriptPath(_options.ScriptPath);
        if (!File.Exists(scriptPath))
        {
            return new GeotagResult(false, 0, 0, $"Python geotag script not found at: {scriptPath}");
        }

        var offsetArg = timeOffsetSeconds.ToString(CultureInfo.InvariantCulture);
        var maxDiffArg = maxDiffSeconds.ToString(CultureInfo.InvariantCulture);
        var arguments = $"\"{scriptPath}\" \"{imagesDirectory}\" \"{csvFilePath}\" --offset {offsetArg} --max-diff {maxDiffArg}";

        logger.LogInformation(
            "Executing Python geotag: {PythonPath} {Arguments}",
            _options.PythonPath,
            arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.PythonPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdoutBuilder.AppendLine(e.Data);
                logger.LogDebug("[Geotag Python] {Output}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderrBuilder.AppendLine(e.Data);
                logger.LogWarning("[Geotag Python Error] {Error}", e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);

            var stdout = stdoutBuilder.ToString();
            var stderr = stderrBuilder.ToString();

            if (process.ExitCode != 0)
            {
                var errorMsg = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                logger.LogError(
                    "Python geotag process exited with code {ExitCode}. Details: {ErrorMessage}",
                    process.ExitCode,
                    errorMsg);

                return new GeotagResult(
                    IsSuccess: false,
                    ProcessedCount: 0,
                    ErrorCount: 1,
                    ErrorMessage: $"Python process exited with code {process.ExitCode}: {errorMsg}");
            }

            logger.LogInformation(
                "Python geotag completed successfully for folder {Folder}.",
                imagesDirectory);

            return new GeotagResult(
                IsSuccess: true,
                ProcessedCount: 1,
                ErrorCount: 0,
                ErrorMessage: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            logger.LogError("Python geotag timed out after {Timeout} seconds.", _options.TimeoutSeconds);
            return new GeotagResult(false, 0, 1, $"Geotag execution timed out after {_options.TimeoutSeconds}s");
        }
        catch (Exception ex)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            logger.LogError(ex, "Unexpected error executing Python geotag script.");
            return new GeotagResult(false, 0, 1, ex.Message);
        }
    }

    private static string ResolveScriptPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        // Check relative to AppContext.BaseDirectory
        var baseCandidate = Path.Combine(AppContext.BaseDirectory, configuredPath);
        if (File.Exists(baseCandidate))
        {
            return baseCandidate;
        }

        // Check relative to current working directory
        var cwdCandidate = Path.Combine(Directory.GetCurrentDirectory(), configuredPath);
        if (File.Exists(cwdCandidate))
        {
            return cwdCandidate;
        }

        // Check solution root candidates (e.g. going up from bin/Debug/net...)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.Exists)
        {
            var testPath = Path.Combine(dir.FullName, configuredPath);
            if (File.Exists(testPath))
            {
                return testPath;
            }

            // Also check backend/scripts/...
            var backendTestPath = Path.Combine(dir.FullName, "backend", configuredPath);
            if (File.Exists(backendTestPath))
            {
                return backendTestPath;
            }

            dir = dir.Parent;
        }

        return configuredPath;
    }
}
