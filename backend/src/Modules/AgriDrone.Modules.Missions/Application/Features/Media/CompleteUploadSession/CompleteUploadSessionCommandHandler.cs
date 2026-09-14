using System.Text.Json;
using AgriDrone.Modules.Missions.Application
    .Abstractions.Media;
using AgriDrone.Modules.Missions.Application
    .Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application
    .Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CompleteUploadSession;

internal sealed class CompleteUploadSessionCommandHandler(
    IDroneMissionRepository missionRepository,
    IMediaUploadSessionRepository uploadSessionRepository,
    IMissionMediaRepository missionMediaRepository,
    IObjectStorage objectStorage,
    IChecksumCalculator checksumCalculator,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        CompleteUploadSessionCommand,
        Result<CompleteUploadSessionResult>>
{
    public async Task<Result<CompleteUploadSessionResult>> Handle(
        CompleteUploadSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<CompleteUploadSessionResult>(
                MissionError.CurrentUserRequired());
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<CompleteUploadSessionResult>(
                MissionError.NotFound(request.MissionId));
        }

        var session = await uploadSessionRepository.GetByIdAsync(
            request.TenantId,
            request.FarmId,
            request.MissionId,
            request.UploadSessionId,
            cancellationToken);

        if (session is null)
        {
            return Result.Failure<CompleteUploadSessionResult>(
                CompleteUploadSessionError.SessionNotFound(
                    request.UploadSessionId));
        }

        if (session.Status ==
            MediaUploadSessionStatus.Completed)
        {
            return Result.Success(
                MapResult(
                    session,
                    reusedCompletion: true));
        }

        if (mission.Status != MissionStatus.Uploading)
        {
            return Result.Failure<CompleteUploadSessionResult>(
                CompleteUploadSessionError
                    .MissionStatusNotAllowed(mission.Status));
        }

        if (session.Status !=
            MediaUploadSessionStatus.Pending)
        {
            return Result.Failure<CompleteUploadSessionResult>(
                CompleteUploadSessionError
                    .SessionStatusNotAllowed(session.Status));
        }

        var previousSessionStatus = session.Status;
        var previousMissionStatus = mission.Status;
        var now = timeProvider.GetUtcNow();

        if (session.IsExpiredAt(now))
        {
            session.MarkExpired(now);
            mission.FailUploading(now);

            return await SaveFailureAsync(
                CompleteUploadSessionError.SessionExpired(),
                cancellationToken);
        }

        session.BeginVerification(now);

        var objectInfo = await objectStorage.GetInfoAsync(
            session.StorageUri,
            cancellationToken);

        if (objectInfo is null)
        {
            session.FailVerification(now);
            mission.FailUploading(now);

            return await SaveFailureAsync(
                CompleteUploadSessionError.ObjectNotFound(),
                cancellationToken);
        }

        if (objectInfo.FileSizeBytes != session.FileSizeBytes)
        {
            session.FailVerification(now);
            mission.FailUploading(now);

            return await SaveFailureAsync(
                CompleteUploadSessionError.FileSizeMismatch(
                    session.FileSizeBytes,
                    objectInfo.FileSizeBytes),
                cancellationToken);
        }

        if (!string.Equals(
                objectInfo.MimeType,
                session.MimeType,
                StringComparison.OrdinalIgnoreCase))
        {
            session.FailVerification(now);
            mission.FailUploading(now);

            return await SaveFailureAsync(
                CompleteUploadSessionError.MimeTypeMismatch(
                    session.MimeType,
                    objectInfo.MimeType),
                cancellationToken);
        }

        string? actualChecksum = null;

        await objectStorage.ReadAsync(
            session.StorageUri,
            async (stream, token) =>
            {
                actualChecksum =
                    await checksumCalculator.CalculateAsync(
                        stream,
                        session.ChecksumAlgorithm,
                        token);
            },
            cancellationToken);

        var verifiedChecksum = actualChecksum
            ?? throw new InvalidOperationException(
                "Object storage reader did not produce " +
                "a checksum.");

        if (!string.Equals(
                verifiedChecksum,
                session.ExpectedChecksum,
                StringComparison.OrdinalIgnoreCase))
        {
            session.FailVerification(now);
            mission.FailUploading(now);

            return await SaveFailureAsync(
                CompleteUploadSessionError.ChecksumMismatch(),
                cancellationToken);
        }

        var mediaAsset = MediaAsset.Create(
            session.MediaAssetId,
            session.TenantId,
            session.FarmId,
            objectInfo.Provider,
            objectInfo.StorageKey,
            session.StorageUri,
            session.MediaType,
            session.MimeType,
            objectInfo.FileSizeBytes,
            verifiedChecksum,
            actorId,
            now);

        var mediaRole = session.MediaType switch
        {
            MediaType.Image =>
                MissionMediaRole.RawImage,

            MediaType.Video =>
                MissionMediaRole.RawVideo,

            _ =>
                MissionMediaRole.Other
        };

        var missionMedia = MissionMedia.Create(
            session.MissionId,
            session.MediaAssetId,
            mediaRole,
            now);

        missionMediaRepository.Add(
            mediaAsset,
            missionMedia);

        session.CompleteVerification(now);

        AddUploadSessionAudit(
            session,
            previousSessionStatus,
            action: "COMPLETE_UPLOAD_VERIFICATION",
            error: null,
            actorId,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
        catch (UploadSessionConcurrencyException)
        {
            return Result.Failure<CompleteUploadSessionResult>(
                CompleteUploadSessionError.ConcurrentUpdate());
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<CompleteUploadSessionResult>(
                CompleteUploadSessionError.ConcurrentUpdate());
        }

        return Result.Success(
            MapResult(
                session,
                reusedCompletion: false));

        async Task<Result<CompleteUploadSessionResult>>
            SaveFailureAsync(
                AppError error,
                CancellationToken token)
        {
            var action = session.Status ==
                MediaUploadSessionStatus.Expired
                    ? "EXPIRE_UPLOAD_SESSION"
                    : "FAIL_UPLOAD_VERIFICATION";

            AddUploadSessionAudit(
                session,
                previousSessionStatus,
                action,
                error,
                actorId,
                now);

            if (previousMissionStatus != mission.Status)
            {
                AddMissionUploadFailureAudit(
                    mission,
                    previousMissionStatus,
                    error,
                    actorId,
                    now);
            }

            try
            {
                await unitOfWork.SaveChangesAsync(token);
            }
            catch (UploadSessionConcurrencyException)
            {
                return Result.Failure<
                    CompleteUploadSessionResult>(
                    CompleteUploadSessionError
                        .ConcurrentUpdate());
            }
            catch (MissionConcurrencyException)
            {
                return Result.Failure<
                    CompleteUploadSessionResult>(
                    CompleteUploadSessionError
                        .ConcurrentUpdate());
            }

            return Result.Failure<
                CompleteUploadSessionResult>(error);
        }
    }

    private void AddUploadSessionAudit(
        MediaUploadSession session,
        MediaUploadSessionStatus previousStatus,
        string action,
        AppError? error,
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
                ErrorCode = error?.Code
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: session.TenantId,
            farmId: session.FarmId,
            actorId: actorId,
            correlationId:
                executionContext.CorrelationId,
            entityType: nameof(MediaUploadSession),
            entityId: session.Id,
            action: action,
            oldData: oldData,
            newData: newData,
            createdAt: changedAt);
    }

    private void AddMissionUploadFailureAudit(
        DroneMission mission,
        MissionStatus previousStatus,
        AppError error,
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
                Status = mission.Status.ToString(),
                ProcessingStatus =
                    mission.ProcessingStatus.ToString(),
                ErrorCode = error.Code
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: mission.TenantId,
            farmId: mission.FarmId,
            actorId: actorId,
            correlationId:
                executionContext.CorrelationId,
            entityType: nameof(DroneMission),
            entityId: mission.Id,
            action: "FAIL_MISSION_UPLOAD",
            oldData: oldData,
            newData: newData,
            createdAt: changedAt);
    }

    private static CompleteUploadSessionResult MapResult(
        MediaUploadSession session,
        bool reusedCompletion)
    {
        return new CompleteUploadSessionResult(
            session.Id,
            session.MediaAssetId,
            session.Status,
            session.MimeType,
            session.FileSizeBytes,
            session.ExpectedChecksum,
            reusedCompletion);
    }
}