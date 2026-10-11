namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

internal sealed class UploadChecksumConflictException(Exception innerException)
    : Exception("An active upload already exists for this Mission checksum.", innerException);
