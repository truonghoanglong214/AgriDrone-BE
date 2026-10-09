using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Media.StartMultipartUpload;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMultipartPartUrl;

internal sealed class GetMultipartPartUrlQueryHandler(
    IDroneMissionRepository missions, IMediaUploadSessionRepository sessions,
    IMultipartObjectStorage storage, ISystemManagerAccessService managerAccess,
    IExecutionContext context, TimeProvider clock)
    : IRequestHandler<GetMultipartPartUrlQuery, Result<GetMultipartPartUrlResult>>
{
    public async Task<Result<GetMultipartPartUrlResult>> Handle(
        GetMultipartPartUrlQuery request, CancellationToken cancellationToken)
    {
        if (context.ActorId is null)
            return Result.Failure<GetMultipartPartUrlResult>(MissionError.CurrentUserRequired());
        var access = await managerAccess.ResolveFarmAccessAsync(request.FarmId,
            cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId ||
            access.FarmId != request.FarmId)
            return Result.Failure<GetMultipartPartUrlResult>(AppError.Forbidden(
                "MediaUpload.FarmAccessDenied", "The manager is not assigned to this Farm."));
        var mission = await missions.GetByIdAsync(request.MissionId,
            request.TenantId, request.FarmId, cancellationToken);
        if (mission is null)
            return Result.Failure<GetMultipartPartUrlResult>(MissionError.NotFound(request.MissionId));
        var session = await sessions.GetByIdAsync(request.TenantId,
            request.FarmId, request.MissionId, request.UploadSessionId,
            cancellationToken);
        if (session is null)
            return Result.Failure<GetMultipartPartUrlResult>(AppError.NotFound(
                "MediaUpload.SessionNotFound", "Upload session was not found."));
        var now = clock.GetUtcNow();
        var count = MultipartUploadPolicy.PartCount(session.FileSizeBytes);
        if (mission.Status != MissionStatus.Uploading ||
            session.Status != MediaUploadSessionStatus.Pending ||
            session.MultipartUploadId is null || session.IsExpiredAt(now))
            return Result.Failure<GetMultipartPartUrlResult>(AppError.Conflict(
                "MediaUpload.MultipartUnavailable", "Multipart upload is not active."));
        if (request.PartNumber < 1 || request.PartNumber > count)
            return Result.Failure<GetMultipartPartUrlResult>(AppError.Validation(
                "MediaUpload.PartNumberInvalid", "Part number is outside the upload range."));

        var lifetime = session.ExpiresAt - now;
        var uri = await storage.CreatePartUploadUriAsync(session.StorageUri,
            session.MultipartUploadId, request.PartNumber, lifetime,
            cancellationToken);
        var size = request.PartNumber == count
            ? session.FileSizeBytes - (long)(count - 1) * MultipartUploadPolicy.PartSizeBytes
            : MultipartUploadPolicy.PartSizeBytes;
        return Result.Success(new GetMultipartPartUrlResult(request.PartNumber,
            size, uri, session.ExpiresAt));
    }
}
