using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedInfrastructure.Auditing;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.StartMultipartUpload;

internal sealed class StartMultipartUploadCommandHandler(
    IDroneMissionRepository missions, IMediaUploadSessionRepository sessions,
    IMultipartObjectStorage storage, IMissionsUnitOfWork unitOfWork,
    ISystemManagerAccessService managerAccess, IExecutionContext context,
    IAuditWriter auditWriter,
    TimeProvider clock)
    : IRequestHandler<StartMultipartUploadCommand, Result<StartMultipartUploadResult>>
{
    public async Task<Result<StartMultipartUploadResult>> Handle(
        StartMultipartUploadCommand request, CancellationToken cancellationToken)
    {
        if (context.ActorId is null)
            return Result.Failure<StartMultipartUploadResult>(MissionError.CurrentUserRequired());
        var access = await managerAccess.ResolveFarmAccessAsync(request.FarmId,
            cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId ||
            access.FarmId != request.FarmId)
            return Result.Failure<StartMultipartUploadResult>(AppError.Forbidden(
                "MediaUpload.FarmAccessDenied", "The manager is not assigned to this Farm."));
        var mission = await missions.GetByIdAsync(request.MissionId,
            request.TenantId, request.FarmId, cancellationToken);
        if (mission is null)
            return Result.Failure<StartMultipartUploadResult>(MissionError.NotFound(request.MissionId));
        var session = await sessions.GetByIdAsync(request.TenantId, request.FarmId,
            request.MissionId, request.UploadSessionId, cancellationToken);
        if (session is null)
            return Result.Failure<StartMultipartUploadResult>(AppError.NotFound(
                "MediaUpload.SessionNotFound", "Upload session was not found."));
        if (mission.Status != MissionStatus.Uploading ||
            session.Status != MediaUploadSessionStatus.Pending ||
            session.IsExpiredAt(clock.GetUtcNow()))
            return Result.Failure<StartMultipartUploadResult>(AppError.Conflict(
                "MediaUpload.MultipartUnavailable", "Upload session is not active."));

        var reused = session.MultipartUploadId is not null;
        if (!reused)
        {
            var uploadId = await storage.InitiateAsync(session.StorageUri,
                session.MimeType, cancellationToken);
            var now = clock.GetUtcNow();
            if (session.IsExpiredAt(now))
            {
                await storage.AbortAsync(session.StorageUri, uploadId,
                    CancellationToken.None);
                return Result.Failure<StartMultipartUploadResult>(AppError.Conflict(
                    "MediaUpload.SessionExpired", "Upload session has expired."));
            }
            session.BeginMultipart(uploadId, now);
            using var auditData = JsonSerializer.SerializeToDocument(new
            {
                session.MissionId, session.OperationId, session.MediaAssetId,
                session.FileSizeBytes, PartSizeBytes = MultipartUploadPolicy.PartSizeBytes
            });
            auditWriter.AddUserAction(unitOfWork, session.TenantId,
                session.FarmId, context.ActorId.Value, context.CorrelationId,
                nameof(MediaUploadSession), session.Id, "START_MULTIPART_UPLOAD",
                null, auditData, now);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UploadSessionConcurrencyException)
            {
                await storage.AbortAsync(session.StorageUri, uploadId,
                    CancellationToken.None);
                return Result.Failure<StartMultipartUploadResult>(AppError.Conflict(
                    "MediaUpload.ConcurrentUpdate", "Retry the multipart operation."));
            }
        }

        var parts = await storage.ListPartsAsync(session.StorageUri,
            session.MultipartUploadId!, cancellationToken);
        return Result.Success(new StartMultipartUploadResult(session.Id,
            MultipartUploadPolicy.PartSizeBytes,
            MultipartUploadPolicy.PartCount(session.FileSizeBytes),
            session.ExpiresAt, parts, reused));
    }
}
