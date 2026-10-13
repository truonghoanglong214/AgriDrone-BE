using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Integrations.Media.Telemetry;

internal sealed class DisabledTelemetryLogNormalizer : ITelemetryLogNormalizer
{
    public Task<Result<NormalizedTelemetryLog>> NormalizeAsync(
        Stream content,
        string sourceFileName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            Result.Failure<NormalizedTelemetryLog>(
                AppError.Conflict(
                    "Telemetry.DecoderDisabled",
                    "Blackbox telemetry decoding is disabled on this server.")));
}
