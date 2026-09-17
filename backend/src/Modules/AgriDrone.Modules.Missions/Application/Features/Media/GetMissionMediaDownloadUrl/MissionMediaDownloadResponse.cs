namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDownloadUrl;

public sealed record MissionMediaDownloadResponse(
    Guid MediaId, string DownloadUrl, DateTimeOffset ExpiresAt);
