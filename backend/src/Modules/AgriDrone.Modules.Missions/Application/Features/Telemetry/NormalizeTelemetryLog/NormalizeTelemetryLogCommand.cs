using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.NormalizeTelemetryLog;

public sealed record NormalizeTelemetryLogCommand(
    Guid FarmId,
    Guid MissionId,
    string SourceFileName,
    Stream Content)
    : IRequest<Result<NormalizedTelemetryLog>>;