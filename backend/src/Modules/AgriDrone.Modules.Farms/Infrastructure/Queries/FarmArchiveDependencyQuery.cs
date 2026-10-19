using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

/// <summary>
/// Authoritative dependency query for archive operations.
/// Fails closed if mission or survey adapters are unavailable.
/// Checks active zones, missions, survey orders, published maps, published results, and retention history.
/// </summary>
internal sealed class LegacyReadOnlyFarmArchiveDependencyQuery(
    FarmsDbContext dbContext,
    IMissionArchiveReferenceQuery missionReferenceQuery,
    ISurveyOrderArchiveReferenceQuery surveyOrderReferenceQuery)
    : IFarmArchiveDependencyQuery
{
    public async Task<ArchiveDependencySummary> GetForZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(missionReferenceQuery);

        var activeMissionCount =
            await missionReferenceQuery.CountActiveForZoneAsync(
                tenantId,
                farmId,
                zoneId,
                cancellationToken);

        var publishedZoneMapCount = await dbContext.ZoneMapVersions
            .AsNoTracking()
            .CountAsync(
                m => m.FarmId == farmId &&
                     m.ZoneId == zoneId &&
                     m.Status == MapVersionStatus.Confirmed,
                cancellationToken);

        return new ArchiveDependencySummary(
            ActiveZoneCount: 0,
            ActiveMissionCount: activeMissionCount,
            ActiveSurveyOrderCount: 0,
            PublishedMapCount: publishedZoneMapCount);
    }

    public async Task<ArchiveDependencySummary> GetForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(missionReferenceQuery);
        ArgumentNullException.ThrowIfNull(surveyOrderReferenceQuery);

        var activeZoneCount = await dbContext.FarmZones
            .AsNoTracking()
            .CountAsync(
                zone =>
                    zone.FarmId == farmId &&
                    zone.Farm.TenantId == tenantId &&
                    zone.DeletedAt == null,
                cancellationToken);

        var activeMissionCount =
            await missionReferenceQuery.CountActiveForFarmAsync(
                tenantId,
                farmId,
                cancellationToken);

        var publishedFarmMapCount = await dbContext.FarmBaseMapVersions
            .AsNoTracking()
            .CountAsync(
                m => m.TenantId == tenantId &&
                     m.FarmId == farmId &&
                     m.Status == FarmBaseMapStatus.Published,
                cancellationToken);

        var activeSurveyOrderCount =
            await surveyOrderReferenceQuery.CountActiveForFarmAsync(
                tenantId,
                farmId,
                cancellationToken);

        var publishedResultCount =
            await surveyOrderReferenceQuery.CountPublishedResultsForFarmAsync(
                tenantId,
                farmId,
                cancellationToken);

        var retentionHistoryCount =
            await surveyOrderReferenceQuery.CountRetentionHistoryForFarmAsync(
                tenantId,
                farmId,
                cancellationToken);

        return new ArchiveDependencySummary(
            ActiveZoneCount: activeZoneCount,
            ActiveMissionCount: activeMissionCount,
            ActiveSurveyOrderCount: activeSurveyOrderCount,
            PublishedMapCount: publishedFarmMapCount,
            PublishedResultCount: publishedResultCount,
            RetentionHistoryCount: retentionHistoryCount);
    }
}
