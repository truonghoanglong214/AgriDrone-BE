namespace AgriDrone.Modules.Missions.Application
    .Abstractions.Media;

internal interface IMissionUploadReadinessQueries
{
    Task<MissionUploadReadinessSnapshot> GetAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);
}