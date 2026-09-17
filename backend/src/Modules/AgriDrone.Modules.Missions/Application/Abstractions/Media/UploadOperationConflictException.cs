namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

internal sealed class UploadOperationConflictException(
    Exception innerException)
    : Exception(
        "An upload session already exists for this operation.",
        innerException);