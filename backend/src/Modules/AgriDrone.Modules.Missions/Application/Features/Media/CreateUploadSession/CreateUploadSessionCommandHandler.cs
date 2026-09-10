using System.Text.Json;
using AgriDrone.IntegrationContracts.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CreateUploadSession;

internal sealed class CreateUploadSessionCommandHandler(
    IDroneMissionRepository missionRepository,
    IMediaUploadSessionRepository uploadSessionRepository,
    IObjectStorage objectStorage,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        CreateUploadSessionCommand,
        Result<CreateUploadSessionResult>>
{
    public async Task<Result<CreateUploadSessionResult>> Handle(
        CreateUploadSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MissionError.CurrentUserRequired());
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MissionError.NotFound(request.MissionId));
        }

        if (!AllowsUpload(mission.Status))
        {
            return Result.Failure<CreateUploadSessionResult>(
                MediaUploadError.MissionStatusNotAllowed(
                    mission.Status));
        }

        var existing =
            await uploadSessionRepository.GetByOperationIdAsync(
                request.TenantId,
                request.FarmId,
                request.MissionId,
                request.OperationId,
                cancellationToken);

        if (existing is not null)
        {
            return await ReuseExistingOperationAsync(
                request,
                mission,
                existing,
                cancellationToken);
        }

        if (mission.Version != request.ExpectedMissionVersion)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MissionError.VersionConflict(
                    request.ExpectedMissionVersion,
                    mission.Version));
        }

        var now = timeProvider.GetUtcNow();
        var mediaAssetId = Guid.NewGuid();

        var objectSession =
            await objectStorage.CreateUploadSessionAsync(
                new ObjectUploadRequest(
                    request.TenantId,
                    mediaAssetId,
                    request.FileName,
                    request.MimeType,
                    request.FileSizeBytes,
                    ChecksumAlgorithms.Sha256,
                    request.Sha256Checksum),
                cancellationToken);

        var uploadSession = MediaUploadSession.Create(
            request.TenantId,
            request.FarmId,
            request.MissionId,
            request.OperationId,
            mediaAssetId,
            actorId,
            request.FileName,
            request.MimeType,
            request.MediaType,
            request.FileSizeBytes,
            request.Sha256Checksum,
            objectSession.StorageUri,
            now,
            objectSession.ExpiresAt);

        var previousMissionStatus = mission.Status;

        if (mission.Status is
            MissionStatus.FlightCompleted or
            MissionStatus.UploadFailed)
        {
            mission.StartUploading(now);
        }

        uploadSessionRepository.Add(uploadSession);

        AddUploadSessionAudit(
            uploadSession,
            previousMissionStatus,
            mission.Status,
            actorId,
            now);

        if (previousMissionStatus != mission.Status)
        {
            AddMissionStatusAudit(
                mission,
                previousMissionStatus,
                actorId,
                now);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MediaUploadError.ConcurrentUpdate());
        }
        catch (UploadSessionConcurrencyException)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MediaUploadError.ConcurrentUpdate());
        }
        catch (UploadOperationConflictException)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MediaUploadError.OperationAlreadyExists());
        }

        return Result.Success(
            MapResult(
                uploadSession,
                objectSession.UploadUri,
                mission.Version,
                reusedOperation: false));
    }

    private async Task<Result<CreateUploadSessionResult>>
        ReuseExistingOperationAsync(
            CreateUploadSessionCommand request,
            DroneMission mission,
            MediaUploadSession existing,
            CancellationToken cancellationToken)
    {
        if (!existing.MatchesRequest(
                request.FileName,
                request.MimeType,
                request.MediaType,
                request.FileSizeBytes,
                request.Sha256Checksum))
        {
            return Result.Failure<CreateUploadSessionResult>(
                MediaUploadError.OperationPayloadConflict(
                    request.OperationId));
        }

        if (mission.Status != MissionStatus.Uploading)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MediaUploadError.RetryOperationRequired());
        }

        if (existing.Status !=
            MediaUploadSessionStatus.Pending)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MediaUploadError.SessionNotPending(
                    existing.Status));
        }

        var now = timeProvider.GetUtcNow();

        var objectSession =
            await objectStorage.CreateUploadSessionAsync(
                new ObjectUploadRequest(
                    existing.TenantId,
                    existing.MediaAssetId,
                    existing.FileName,
                    existing.MimeType,
                    existing.FileSizeBytes,
                    existing.ChecksumAlgorithm,
                    existing.ExpectedChecksum),
                cancellationToken);

        existing.RefreshPendingUpload(
            objectSession.StorageUri,
            objectSession.ExpiresAt,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
        catch (UploadSessionConcurrencyException)
        {
            return Result.Failure<CreateUploadSessionResult>(
                MediaUploadError.ConcurrentUpdate());
        }

        return Result.Success(
            MapResult(
                existing,
                objectSession.UploadUri,
                mission.Version,
                reusedOperation: true));
    }

    private void AddUploadSessionAudit(
        MediaUploadSession session,
        MissionStatus previousMissionStatus,
        MissionStatus currentMissionStatus,
        Guid actorId,
        DateTimeOffset createdAt)
    {
        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                session.MissionId,
                session.OperationId,
                session.MediaAssetId,
                session.FileName,
                session.MimeType,
                MediaType = session.MediaType.ToString(),
                session.FileSizeBytes,
                session.ChecksumAlgorithm,
                session.ExpectedChecksum,
                Status = session.Status.ToString(),
                PreviousMissionStatus =
                    previousMissionStatus.ToString(),
                CurrentMissionStatus =
                    currentMissionStatus.ToString(),
                session.ExpiresAt
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: session.TenantId,
            farmId: session.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(MediaUploadSession),
            entityId: session.Id,
            action: "CREATE_UPLOAD_SESSION",
            oldData: null,
            newData: newData,
            createdAt: createdAt);
    }

    private void AddMissionStatusAudit(
        DroneMission mission,
        MissionStatus previousStatus,
        Guid actorId,
        DateTimeOffset changedAt)
    {
        using var oldData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = previousStatus.ToString()
            });

        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = mission.Status.ToString()
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: mission.TenantId,
            farmId: mission.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(DroneMission),
            entityId: mission.Id,
            action: "START_UPLOADING",
            oldData: oldData,
            newData: newData,
            createdAt: changedAt);
    }

    private static bool AllowsUpload(
        MissionStatus status)
    {
        return status is
            MissionStatus.FlightCompleted or
            MissionStatus.Uploading or
            MissionStatus.UploadFailed;
    }

    private static CreateUploadSessionResult MapResult(
        MediaUploadSession session,
        Uri uploadUri,
        uint missionVersion,
        bool reusedOperation)
    {
        return new CreateUploadSessionResult(
            session.Id,
            session.MediaAssetId,
            uploadUri,
            session.ExpiresAt,
            session.Status,
            missionVersion,
            reusedOperation);
    }
}