using System.Globalization;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application
    .Abstractions.Missions;
using AgriDrone.Modules.Missions.Application
    .Abstractions.Telemetry;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application
    .Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;

internal sealed class ImportTelemetryCommandHandler(
    IDroneMissionRepository missionRepository,
    IMissionTelemetryRepository telemetryRepository,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<
        ImportTelemetryCommand,
        Result<ImportTelemetryResult>>
{
    private static readonly JsonSerializerOptions SegmentJsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<Result<ImportTelemetryResult>> Handle(
        ImportTelemetryCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ImportTelemetryResult>(
                MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId ||
            access.FarmId != request.FarmId)
            return Result.Failure<ImportTelemetryResult>(AppError.Forbidden(
                "MissionTelemetry.FarmAccessDenied", "The manager is not assigned to this Farm."));

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<ImportTelemetryResult>(
                MissionError.NotFound(request.MissionId));
        }

        if (mission.SurveyOrderId is null)
            return Result.Failure<ImportTelemetryResult>(
                AppError.Conflict("MissionTelemetry.MissionNotOrderBound",
                    "Legacy Missions cannot accept new telemetry."));

        var orderedInput = request.Segments
            .SelectMany(segment => segment.Points)
            .OrderBy(point => point.RecordedAt)
            .Select((point, sequenceNumber) =>
                point with { SequenceNumber = sequenceNumber })
            .ToArray();

        using var selectedSegmentJson =
            CreateSelectedSegmentJson(
                orderedInput);

        var existingOperation =
            await telemetryRepository
                .GetImportByOperationIdAsync(
                    request.TenantId,
                    request.FarmId,
                    request.MissionId,
                    request.OperationId,
                    cancellationToken);

        if (existingOperation is not null &&
            (existingOperation.MatchesPayload(
                 request.SourceFileName,
                 request.SourceChecksum,
                 orderedInput.Length,
                 selectedSegmentJson) ||
             (existingOperation.SelectedSegmentJson is null &&
              mission.Status != MissionStatus.Uploading &&
              existingOperation.MatchesPayload(
                  request.SourceFileName,
                  request.SourceChecksum,
                  orderedInput.Length))))
        {
            return Result.Success(
                MapResult(
                    existingOperation,
                    mission.Version,
                    reusedOperation: true));
        }

        if (mission.Status is not MissionStatus.FlightCompleted and
            not MissionStatus.Uploading)
        {
            return Result.Failure<ImportTelemetryResult>(
                ImportTelemetryError
                    .MissionStatusNotAllowed(
                        mission.Status));
        }

        if (mission.Version !=
            request.ExpectedMissionVersion)
        {
            return Result.Failure<ImportTelemetryResult>(
                MissionError.VersionConflict(
                    request.ExpectedMissionVersion,
                    mission.Version));
        }

        var existingMissionImport =
            await telemetryRepository
                .GetImportByMissionAsync(
                    request.TenantId,
                    request.FarmId,
                    request.MissionId,
                    cancellationToken);

        if (mission.StartedAt is not DateTimeOffset startedAt ||
            mission.EndedAt is not DateTimeOffset endedAt)
        {
            return Result.Failure<ImportTelemetryResult>(
                ImportTelemetryError
                    .FlightTimelineMissing());
        }

        if (orderedInput[0].RecordedAt < startedAt ||
            orderedInput[^1].RecordedAt > endedAt)
        {
            return Result.Failure<ImportTelemetryResult>(
                ImportTelemetryError
                    .TimestampOutsideMission());
        }

        var now = timeProvider.GetUtcNow();

        if (mission.Status == MissionStatus.FlightCompleted)
            mission.StartUploading(now);

        var telemetryPoints = orderedInput
            .Select(point =>
                MissionTelemetryPoint.Create(
                    request.MissionId,
                    point.SequenceNumber,
                    point.RecordedAt,
                    point.Longitude,
                    point.Latitude,
                    point.AltitudeM,
                    point.AltitudeReference,
                    point.HeadingDeg,
                    point.SpeedMps,
                    point.HorizontalAccuracyM,
                    now))
            .ToArray();

        var actualFlightRoute =
            TelemetryRouteFactory.Create(
                telemetryPoints);

        var telemetryImport =
            MissionTelemetryImport.Create(
                request.TenantId,
                request.FarmId,
                request.MissionId,
                request.OperationId,
                request.SourceFileName,
                request.SourceChecksum,
                telemetryPoints.Length,
                selectedSegmentJson,
                telemetryPoints[0].RecordedAt,
                telemetryPoints[^1].RecordedAt,
                actorId,
                now);

        var previousOperationId =
            existingMissionImport?.OperationId;
        var persistedImport =
            existingMissionImport ?? telemetryImport;

        existingMissionImport?.ReplaceWith(telemetryImport);

        mission.SetActualFlightRoute(
            actualFlightRoute,
            now);

        if (existingMissionImport is null)
        {
            telemetryRepository.Add(
                telemetryImport,
                telemetryPoints);
        }

        AddAudit(
            persistedImport,
            actorId,
            now,
            previousOperationId);

        try
        {
            if (existingMissionImport is null)
            {
                await unitOfWork.SaveChangesAsync(
                    cancellationToken);
            }
            else
            {
                await telemetryRepository.ReplaceAsync(
                    existingMissionImport,
                    telemetryPoints,
                    cancellationToken);
            }
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<ImportTelemetryResult>(
                ImportTelemetryError.ConcurrentUpdate());
        }
        catch (TelemetryImportConflictException)
        {
            return Result.Failure<ImportTelemetryResult>(
                ImportTelemetryError.ConcurrentUpdate());
        }

        return Result.Success(
            MapResult(
                persistedImport,
                mission.Version,
                reusedOperation: false));
    }

    internal static JsonDocument CreateSelectedSegmentJson(
        IReadOnlyList<ImportTelemetryPoint> points)
    {
        return JsonSerializer.SerializeToDocument(
            new
            {
                segmentIndex = (int?)null,
                points = points.Select(point => new
                {
                    timestamp = point.RecordedAt
                        .ToOffset(TimeSpan.FromHours(7))
                        .ToString(
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture),
                    lat = point.Latitude,
                    lon = point.Longitude,
                    alt = point.AltitudeM
                }).ToArray()
            },
            SegmentJsonOptions);
    }

    private void AddAudit(
        MissionTelemetryImport telemetryImport,
        Guid actorId,
        DateTimeOffset importedAt,
        Guid? previousOperationId)
    {
        using var oldData = previousOperationId is Guid operationId
            ? JsonSerializer.SerializeToDocument(new { OperationId = operationId })
            : null;

        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                telemetryImport.MissionId,
                telemetryImport.OperationId,
                telemetryImport.SourceFileName,
                telemetryImport.PayloadChecksum,
                telemetryImport.PointCount,
                telemetryImport.FirstRecordedAt,
                telemetryImport.LastRecordedAt,
                FlightRouteSrid = 4326
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: telemetryImport.TenantId,
            farmId: telemetryImport.FarmId,
            actorId: actorId,
            correlationId:
                executionContext.CorrelationId,
            entityType:
                nameof(MissionTelemetryImport),
            entityId: telemetryImport.Id,
            action: previousOperationId.HasValue
                ? "REPLACE_MISSION_TELEMETRY"
                : "IMPORT_MISSION_TELEMETRY",
            oldData: oldData,
            newData: newData,
            createdAt: importedAt);
    }

    private static ImportTelemetryResult MapResult(
        MissionTelemetryImport telemetryImport,
        uint missionVersion,
        bool reusedOperation)
    {
        return new ImportTelemetryResult(
            telemetryImport.Id,
            telemetryImport.MissionId,
            telemetryImport.PointCount,
            telemetryImport.FirstRecordedAt,
            telemetryImport.LastRecordedAt,
            missionVersion,
            reusedOperation);
    }
}
