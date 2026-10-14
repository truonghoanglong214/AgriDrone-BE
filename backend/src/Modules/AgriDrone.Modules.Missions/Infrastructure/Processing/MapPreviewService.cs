using System.Diagnostics;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class MapPreviewService(
    IOptions<MapPreviewOptions> options,
    ILogger<MapPreviewService> logger) : IMapPreviewService
{
    private readonly MapPreviewOptions _options = options.Value;

    public async Task<MapPreviewResult> GeneratePreviewAsync(
        string inputTifPath,
        string outputJpgPath,
        int maxSize = 2048,
        int quality = 85,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputTifPath))
        {
            return new MapPreviewResult(
                false, null, 0, 0, 0, 0, null, null,
                $"Input GeoTIFF not found: {inputTifPath}");
        }

        var scriptPath = ResolveScriptPath(_options.ScriptPath);
        if (!File.Exists(scriptPath))
        {
            return new MapPreviewResult(
                false, null, 0, 0, 0, 0, null, null,
                $"Map preview script not found at resolved path: {scriptPath}");
        }

        var arguments = $"\"{scriptPath}\" --input \"{inputTifPath}\" --output \"{outputJpgPath}\" --max-size {maxSize} --quality {quality}";

        logger.LogInformation(
            "Executing map preview script: {Python} {Arguments}",
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

        try
        {
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                logger.LogError(
                    "Map preview script exited with code {ExitCode}. Error: {Error}",
                    process.ExitCode,
                    stderr);

                return new MapPreviewResult(
                    false, null, 0, 0, 0, 0, null, null,
                    $"Script failed with exit code {process.ExitCode}: {stderr}");
            }

            return ParseOutput(stdout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillProcessSafely(process);
            logger.LogError("Map preview generation timed out after {Timeout} seconds.", _options.TimeoutSeconds);
            return new MapPreviewResult(false, null, 0, 0, 0, 0, null, null, "Operation timed out.");
        }
        catch (Exception ex)
        {
            KillProcessSafely(process);
            logger.LogError(ex, "Failed to run map preview script.");
            return new MapPreviewResult(false, null, 0, 0, 0, 0, null, null, ex.Message);
        }
    }

    private static MapPreviewResult ParseOutput(string stdout)
    {
        try
        {
            using var doc = JsonDocument.Parse(stdout.Trim());
            var root = doc.RootElement;

            var success = root.GetProperty("success").GetBoolean();
            var previewPath = root.GetProperty("preview_path").GetString();
            var origW = root.GetProperty("original_width").GetInt32();
            var origH = root.GetProperty("original_height").GetInt32();
            var prevW = root.GetProperty("preview_width").GetInt32();
            var prevH = root.GetProperty("preview_height").GetInt32();

            var boundsElem = root.GetProperty("bounds");
            var bounds = new MapBounds(
                boundsElem.GetProperty("min_lat").GetDouble(),
                boundsElem.GetProperty("max_lat").GetDouble(),
                boundsElem.GetProperty("min_lon").GetDouble(),
                boundsElem.GetProperty("max_lon").GetDouble());

            var cornersElem = root.GetProperty("corners");
            var nw = cornersElem.GetProperty("north_west");
            var ne = cornersElem.GetProperty("north_east");
            var se = cornersElem.GetProperty("south_east");
            var sw = cornersElem.GetProperty("south_west");

            var corners = new MapCorners(
                new MapCoordinate(nw[0].GetDouble(), nw[1].GetDouble()),
                new MapCoordinate(ne[0].GetDouble(), ne[1].GetDouble()),
                new MapCoordinate(se[0].GetDouble(), se[1].GetDouble()),
                new MapCoordinate(sw[0].GetDouble(), sw[1].GetDouble()));

            return new MapPreviewResult(
                success, previewPath, origW, origH, prevW, prevH, bounds, corners, null);
        }
        catch (Exception ex)
        {
            return new MapPreviewResult(false, null, 0, 0, 0, 0, null, null, $"Failed to parse script output: {ex.Message}");
        }
    }

    private static string ResolveScriptPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath)) return configuredPath;

        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, configuredPath);
        if (File.Exists(candidate)) return candidate;

        var currentDir = Directory.GetCurrentDirectory();
        candidate = Path.Combine(currentDir, configuredPath);
        if (File.Exists(candidate)) return candidate;

        candidate = Path.Combine(currentDir, "backend", configuredPath);
        if (File.Exists(candidate)) return candidate;

        var dir = new DirectoryInfo(currentDir);
        while (dir is not null)
        {
            var p1 = Path.Combine(dir.FullName, configuredPath);
            if (File.Exists(p1)) return p1;

            var p2 = Path.Combine(dir.FullName, "backend", configuredPath);
            if (File.Exists(p2)) return p2;

            dir = dir.Parent;
        }

        return configuredPath;
    }

    private static void KillProcessSafely(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ignore error on kill
        }
    }
}
