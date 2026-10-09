namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMultipartPartUrl;

public sealed record GetMultipartPartUrlResult(int PartNumber, long SizeBytes,
    Uri UploadUri, DateTimeOffset ExpiresAt);
