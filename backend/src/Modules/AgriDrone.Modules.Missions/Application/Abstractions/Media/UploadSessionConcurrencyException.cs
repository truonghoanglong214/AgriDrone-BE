namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

internal sealed class UploadSessionConcurrencyException(
    Exception innerException)
    : Exception(
        "The upload session was changed by another request.",
        innerException);