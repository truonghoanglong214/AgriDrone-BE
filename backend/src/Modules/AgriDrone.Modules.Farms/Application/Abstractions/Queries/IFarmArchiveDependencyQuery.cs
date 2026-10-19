namespace AgriDrone.Modules.Farms.Application.Abstractions.Queries;

public interface IFarmArchiveDependencyQuery
{
    Task<ArchiveDependencySummary> GetForZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default);

    Task<ArchiveDependencySummary> GetForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default);
}

public sealed record ArchiveDependencySummary(
    int ActiveZoneCount,
    int ActiveMissionCount,
    int ActiveSurveyOrderCount = 0,
    int PublishedMapCount = 0,
    int PublishedResultCount = 0,
    int RetentionHistoryCount = 0,
    IReadOnlyList<string>? StableReasonCodes = null)
{
    public IReadOnlyList<string> StableReasonCodes { get; init; } =
        StableReasonCodes ?? BuildReasonCodes(
            ActiveZoneCount,
            ActiveMissionCount,
            ActiveSurveyOrderCount,
            PublishedMapCount,
            PublishedResultCount,
            RetentionHistoryCount);

    public bool HasAny =>
        ActiveZoneCount > 0 ||
        ActiveMissionCount > 0 ||
        ActiveSurveyOrderCount > 0 ||
        PublishedMapCount > 0 ||
        PublishedResultCount > 0 ||
        RetentionHistoryCount > 0;

    private static List<string> BuildReasonCodes(
        int activeZoneCount,
        int activeMissionCount,
        int activeSurveyOrderCount,
        int publishedMapCount,
        int publishedResultCount,
        int retentionHistoryCount)
    {
        var reasons = new List<string>();
        if (activeZoneCount > 0) reasons.Add("ACTIVE_ZONES_EXIST");
        if (activeMissionCount > 0) reasons.Add("ACTIVE_MISSIONS_EXIST");
        if (activeSurveyOrderCount > 0) reasons.Add("ACTIVE_SURVEY_ORDERS_EXIST");
        if (publishedMapCount > 0) reasons.Add("PUBLISHED_MAP_EXISTS");
        if (publishedResultCount > 0) reasons.Add("PUBLISHED_RESULTS_EXIST");
        if (retentionHistoryCount > 0) reasons.Add("RETENTION_HISTORY_ACTIVE");
        return reasons;
    }
}
