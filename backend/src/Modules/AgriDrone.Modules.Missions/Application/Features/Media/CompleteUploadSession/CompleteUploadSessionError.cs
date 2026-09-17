using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CompleteUploadSession;

internal static class CompleteUploadSessionError
{
    public static AppError SessionNotFound(Guid sessionId) =>
        AppError.NotFound(
            "MediaUpload.SessionNotFound",
            $"Upload session '{sessionId}' was not found.");

    public static AppError MissionStatusNotAllowed(
        MissionStatus status) =>
        AppError.Conflict(
            "MediaUpload.MissionStatusNotAllowed",
            $"Mission in status '{status}' cannot complete an upload.");

    public static AppError SessionStatusNotAllowed(
        MediaUploadSessionStatus status) =>
        AppError.Conflict(
            "MediaUpload.SessionStatusNotAllowed",
            $"Upload session in status '{status}' cannot be completed.");

    public static AppError SessionExpired() =>
        AppError.Conflict(
            "MediaUpload.SessionExpired",
            "The upload session has expired.");

    public static AppError ObjectNotFound() =>
        AppError.Validation(
            "MediaUpload.ObjectNotFound",
            "The uploaded object was not found in object storage.");

    public static AppError FileSizeMismatch(
        long expected,
        long actual) =>
        AppError.Validation(
            "MediaUpload.FileSizeMismatch",
            $"Expected {expected} bytes but object storage contains {actual} bytes.");

    public static AppError MimeTypeMismatch(
        string expected,
        string actual) =>
        AppError.Validation(
            "MediaUpload.MimeTypeMismatch",
            $"Expected MIME type '{expected}' but received '{actual}'.");

    public static AppError ChecksumMismatch() =>
        AppError.Validation(
            "MediaUpload.ChecksumMismatch",
            "The uploaded object's SHA256 checksum does not match.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "MediaUpload.ConcurrentUpdate",
            "The upload session changed concurrently. Retry the request.");
}