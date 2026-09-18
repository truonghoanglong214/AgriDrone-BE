using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.NormalizeTelemetryLog;

internal sealed class NormalizeTelemetryLogCommandHandler(
    IDroneMissionRepository missions,
    ITelemetryLogNormalizer normalizer,
    IExecutionContext executionContext)
    : IRequestHandler<
        NormalizeTelemetryLogCommand,
        Result<NormalizedTelemetryLog>>
{
    public async Task<Result<NormalizedTelemetryLog>> Handle(
        NormalizeTelemetryLogCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<NormalizedTelemetryLog>(
                MissionError.CurrentTenantRequired());
        }

        if (executionContext.ActorId is null)
        {
            return Result.Failure<NormalizedTelemetryLog>(
                MissionError.CurrentUserRequired());
        }

        var mission = await missions.GetByIdAsync(
            request.MissionId,
            tenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<NormalizedTelemetryLog>(
                MissionError.NotFound(request.MissionId));
        }

        return await normalizer.NormalizeAsync(
            request.Content,
            request.SourceFileName,
            cancellationToken);
    }
}