using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;

public interface ITelemetryLogNormalizer
{
    Task<Result<NormalizedTelemetryLog>> NormalizeAsync(
        Stream content,
        string sourceFileName,
        CancellationToken cancellationToken = default);
}