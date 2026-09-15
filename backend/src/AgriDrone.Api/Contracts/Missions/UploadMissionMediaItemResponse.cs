namespace AgriDrone.Api.Contracts.Missions;

public sealed record UploadMissionMediaItemResponse(
    int Index,
    string FileName,
    string Status,
    CompleteMissionMediaUploadSessionResponse? Media,
    string? ErrorCode,
    string? ErrorMessage);
