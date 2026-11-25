namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public enum MissionComboAnalysisPublishStatus
{
    Queued,
    NotFound,
    Conflict,
    Invalid
}

public sealed record MissionComboAnalysisPublishResult(
    MissionComboAnalysisPublishStatus Status,
    Guid? HandoffId = null,
    Guid? MessageId = null,
    IReadOnlyCollection<string>? Errors = null);

public interface IMissionComboAnalysisPublisher
{
    Task<MissionComboAnalysisPublishResult> PublishAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid jobId,
        int sourceRevision,
        Guid actorId,
        Guid correlationId,
        Guid idempotencyKey,
        CancellationToken cancellationToken);
}
