using System.Text.Json;
using AgriDrone.Modules.Missions.Application
    .Abstractions.Missions;
using AgriDrone.Modules.Missions.Application
    .Abstractions.Telemetry;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
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
    TimeProvider timeProvider)
    : IRequestHandler<
        ImportTelemetryCommand,
        Result<ImportTelemetryResult>>
{
    public async Task<Result<ImportTelemetryResult>> Handle(
        ImportTelemetryCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ImportTelemetryResult>(
                MissionError.CurrentUserRequired());
        }

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

        var existingOperation =
            await telemetryRepository
                .GetImportByOperationIdAsync(
                    request.TenantId,
                    request.FarmId,
                    request.MissionId,
                    request.OperationId,
                    cancellationToken);

        if (existingOperation is not null)
        {
            if (!existingOperation.MatchesPayload(
                    request.SourceFileName,
                    request.SourceChecksum,
                    request.Points.Count))
            {
                return Result.Failure<ImportTelemetryResult>(
                    ImportTelemetryError
                        .OperationPayloadConflict(
                            request.OperationId));
            }

            return Result.Success(
                MapResult(
                    existingOperation,
                    mission.Version,
                    reusedOperation: true));
        }

        if (mission.Status != MissionStatus.Uploading)
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

        if (existingMissionImport is not null)
        {
            return Result.Failure<ImportTelemetryResult>(
                ImportTelemetryError
                    .MissionAlreadyImported(
                        existingMissionImport.OperationId));
        }

        if (mission.StartedAt is not DateTimeOffset startedAt ||
            mission.EndedAt is not DateTimeOffset endedAt)
        {
            return Result.Failure<ImportTelemetryResult>(
                ImportTelemetryError
                    .FlightTimelineMissing());
        }

        var orderedInput = request.Points
            .OrderBy(point => point.SequenceNumber)
            .ToArray();

        if (orderedInput[0].RecordedAt < startedAt ||
            orderedInput[^1].RecordedAt > endedAt)
        {
            return Result.Failure<ImportTelemetryResult>(
                ImportTelemetryError
                    .TimestampOutsideMission());
        }

        var now = timeProvider.GetUtcNow();

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
                telemetryPoints[0].RecordedAt,
                telemetryPoints[^1].RecordedAt,
                actorId,
                now);

        mission.SetActualFlightRoute(
            actualFlightRoute,
            now);

        telemetryRepository.Add(
            telemetryImport,
            telemetryPoints);

        AddAudit(
            telemetryImport,
            actorId,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(
                cancellationToken);
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
                telemetryImport,
                mission.Version,
                reusedOperation: false));
    }

    private void AddAudit(
        MissionTelemetryImport telemetryImport,
        Guid actorId,
        DateTimeOffset importedAt)
    {
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
            action: "IMPORT_MISSION_TELEMETRY",
            oldData: null,
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