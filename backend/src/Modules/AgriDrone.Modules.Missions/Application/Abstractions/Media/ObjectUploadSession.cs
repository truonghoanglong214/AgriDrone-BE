namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

public sealed record ObjectUploadSession(
    string StorageUri,
    Uri UploadUri,
    DateTimeOffset ExpiresAt);