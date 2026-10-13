namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

public sealed record MultipartUploadedPart(int Number, long SizeBytes, string ETag);
