using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;

public sealed record ImportTelemetryCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    Guid OperationId,
    string SourceFileName,
    string SourceChecksum,
    uint ExpectedMissionVersion,
    IReadOnlyList<ImportTelemetryPoint> Points)
    : IRequest<Result<ImportTelemetryResult>>;