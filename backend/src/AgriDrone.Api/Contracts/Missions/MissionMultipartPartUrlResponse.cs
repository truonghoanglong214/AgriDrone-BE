namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionMultipartPartUrlResponse(
    int PartNumber, long SizeBytes, string UploadUri,
    DateTimeOffset ExpiresAt);
