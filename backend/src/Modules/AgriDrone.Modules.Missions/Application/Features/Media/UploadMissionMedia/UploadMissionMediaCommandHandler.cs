using System.Security.Cryptography;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Media.CompleteUploadSession;
using AgriDrone.Modules.Missions.Application.Features.Media.CreateUploadSession;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.UploadMissionMedia;

internal sealed class UploadMissionMediaCommandHandler(
    IDroneMissionRepository missions,
    IMediaUploadSessionRepository sessions,
    IObjectStorageWriter writer,
    IExecutionContext executionContext,
    ISender sender)
    : IRequestHandler<UploadMissionMediaCommand, Result<CompleteUploadSessionResult>>
{
    public async Task<Result<CompleteUploadSessionResult>> Handle(
        UploadMissionMediaCommand request, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId != request.TenantId || executionContext.ActorId is null)
            return Result.Failure<CompleteUploadSessionResult>(MissionError.CurrentTenantRequired());

        var mission = await missions.GetByIdAsync(request.MissionId,
            request.TenantId, request.FarmId, cancellationToken);
        if (mission is null)
            return Result.Failure<CompleteUploadSessionResult>(MissionError.NotFound(request.MissionId));

        // Multipart files are buffered by ASP.NET, spilling large files to disk.
        var content = request.Content;
        if (!content.CanRead || !content.CanSeek || content.Length == 0)
            return InvalidFile("A non-empty, seekable file is required.");

        content.Position = 0;
        var header = new byte[12];
        var headerLength = await content.ReadAtLeastAsync(header, header.Length,
            throwOnEndOfStream: false, cancellationToken: cancellationToken);
        var mimeType = UploadedMediaFormat.Detect(header.AsSpan(0, headerLength));
        if (mimeType is null)
            return InvalidFile("Only JPEG, PNG, MP4 and MOV file signatures are supported.");

        var mediaType = mimeType.StartsWith("image/", StringComparison.Ordinal)
            ? MediaType.Image : MediaType.Video;
        var limit = mediaType == MediaType.Image ? 50L * 1024 * 1024 : 5L * 1024 * 1024 * 1024;
        if (content.Length > limit)
            return InvalidFile("Images must be at most 50 MiB and videos at most 5 GiB.");

        content.Position = 0;
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        var checksum = Convert.ToHexString(hash).ToLowerInvariant();
        // Operation uniqueness is scoped by tenant/farm/mission in persistence.
        var operationId = new Guid(hash.AsSpan(0, 16));
        var fileName = checksum + UploadedMediaFormat.Extension(mimeType);
        var existing = await sessions.GetByOperationIdAsync(request.TenantId,
            request.FarmId, request.MissionId, operationId, cancellationToken);
        if (existing is not null && !existing.MatchesRequest(fileName, mimeType,
                mediaType, content.Length, checksum))
            return Result.Failure<CompleteUploadSessionResult>(
                MediaUploadError.OperationPayloadConflict(operationId));

        if (existing?.Status == MediaUploadSessionStatus.Completed)
            return await Complete(existing.Id);

        var created = await sender.Send(new CreateUploadSessionCommand(
            request.TenantId, request.FarmId, request.MissionId, operationId,
            fileName, mimeType, mediaType, content.Length, checksum, mission.Version),
            cancellationToken);
        if (created.IsFailure)
            return Result.Failure<CompleteUploadSessionResult>(created.Error);

        var session = await sessions.GetByIdAsync(request.TenantId, request.FarmId,
            request.MissionId, created.Value.UploadSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Persisted upload session was not found.");
        content.Position = 0;
        // Transfer failures leave a tracked Pending session for a subsequent retry.
        await writer.UploadAsync(session.StorageUri, content, content.Length,
            mimeType, cancellationToken);
        return await Complete(session.Id);

        Task<Result<CompleteUploadSessionResult>> Complete(Guid sessionId) =>
            sender.Send(new CompleteUploadSessionCommand(request.TenantId,
                request.FarmId, request.MissionId, sessionId), cancellationToken);
    }

    private static Result<CompleteUploadSessionResult> InvalidFile(string message) =>
        Result.Failure<CompleteUploadSessionResult>(
            AppError.Validation("MediaUpload.InvalidFile", message));
}
