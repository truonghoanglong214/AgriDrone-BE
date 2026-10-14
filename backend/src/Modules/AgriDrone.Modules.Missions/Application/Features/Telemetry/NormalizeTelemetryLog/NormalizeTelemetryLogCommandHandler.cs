using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.NormalizeTelemetryLog;

internal sealed class NormalizeTelemetryLogCommandHandler(
    IDroneMissionRepository missions,
    IMissionTelemetryRepository telemetryRepository,
    IMissionsUnitOfWork unitOfWork,
    ITelemetryLogNormalizer normalizer,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<
        NormalizeTelemetryLogCommand,
        Result<NormalizedTelemetryLog>>
{
    public async Task<Result<NormalizedTelemetryLog>> Handle(
        NormalizeTelemetryLogCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<NormalizedTelemetryLog>(
                MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != request.FarmId)
        {
            return Result.Failure<NormalizedTelemetryLog>(AppError.Forbidden(
                "MissionTelemetry.FarmAccessDenied",
                "The manager is not assigned to this Farm."));
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

        var normalized = await normalizer.NormalizeAsync(
            request.Content,
            request.SourceFileName,
            cancellationToken);
        if (normalized.IsFailure)
            return normalized;

        var value = normalized.Value;
        var existing = await telemetryRepository.GetDecodeAsync(
            tenantId, request.FarmId, request.MissionId,
            value.SourceChecksum, cancellationToken);
        if (existing is not null)
            return Result.Success(value with { DecodeId = existing.Id });

        using var pointsJson = JsonSerializer.SerializeToDocument(new
        {
            segments = value.Segments.Select(segment => new
            {
                segmentIndex = segment.SegmentIndex,
                points = segment.Points.Select(point => new
                {
                    timestamp = point.RecordedAt,
                    lat = point.Latitude,
                    lon = point.Longitude,
                    alt = point.AltitudeM
                }).ToArray()
            }).ToArray()
        });

        var decode = BlackboxDecode.Create(
            tenantId, request.FarmId, request.MissionId,
            value.SourceFileName, value.SourceChecksum, pointsJson,
            actorId, timeProvider.GetUtcNow());
        telemetryRepository.AddDecode(decode);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (BlackboxDecodeConflictException)
        {
            var concurrent = await telemetryRepository.GetDecodeAsync(
                tenantId, request.FarmId, request.MissionId,
                value.SourceChecksum, cancellationToken);
            if (concurrent is not null)
                return Result.Success(value with { DecodeId = concurrent.Id });
            throw;
        }

        return Result.Success(value with { DecodeId = decode.Id });
    }
}
