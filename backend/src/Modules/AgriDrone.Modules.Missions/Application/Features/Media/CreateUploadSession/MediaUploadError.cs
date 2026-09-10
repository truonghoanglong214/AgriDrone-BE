using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CreateUploadSession;

internal static class MediaUploadError
{
    public static AppError MissionStatusNotAllowed(
        MissionStatus status) =>
        AppError.Conflict(
            "MediaUpload.MissionStatusNotAllowed",
            $"Mission in status '{status}' does not allow media upload.");

    public static AppError OperationPayloadConflict(
        Guid operationId) =>
        AppError.Conflict(
            "MediaUpload.OperationPayloadConflict",
            $"Operation '{operationId}' already exists with different file information.");

    public static AppError SessionNotPending(
        MediaUploadSessionStatus status) =>
        AppError.Conflict(
            "MediaUpload.SessionNotPending",
            $"Upload session in status '{status}' cannot receive another upload URL.");

    public static AppError RetryOperationRequired() =>
        AppError.Conflict(
            "MediaUpload.NewOperationRequired",
            "The previous mission upload attempt failed. " +
            "Create a new operation ID for the retry.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "MediaUpload.ConcurrentUpdate",
            "The mission or upload session changed concurrently. Retry the request.");

    public static AppError OperationAlreadyExists() =>
        AppError.Conflict(
            "MediaUpload.OperationAlreadyExists",
            "Another request created this upload operation. Retry with the same operation ID.");
}