using AgriDrone.Modules.Plants.Domain.Changes;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Repositories;

internal sealed class PlantInventoryChangeReportRepository(PlantsDbContext context)
    : IPlantInventoryChangeReportRepository
{
    private static readonly PlantInventoryChangeStatus[] OpenStatuses =
    [
        PlantInventoryChangeStatus.Submitted,
        PlantInventoryChangeStatus.UnderReview,
        PlantInventoryChangeStatus.AwaitingSurveyEvidence,
        PlantInventoryChangeStatus.Verified
    ];

    public Task<PlantInventoryChangeReport?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid reportId,
        CancellationToken cancellationToken = default) =>
        context.PlantInventoryChangeReports.SingleOrDefaultAsync(
            report => report.TenantId == tenantId &&
                      report.FarmId == farmId &&
                      report.Id == reportId,
            cancellationToken);

    public Task<PlantInventoryChangeReport?> GetByIdempotencyAsync(
        string callerScope,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var normalizedScope = callerScope.Trim().ToLowerInvariant();
        var normalizedKey = idempotencyKey.Trim();

        return context.PlantInventoryChangeReports.SingleOrDefaultAsync(
            report => report.CallerScope == normalizedScope &&
                      report.IdempotencyKey == normalizedKey,
            cancellationToken);
    }

    public Task<bool> HasOpenReportForPlantAsync(
        Guid farmId,
        Guid plantId,
        CancellationToken cancellationToken = default) =>
        context.PlantInventoryChangeReports
            .AsNoTracking()
            .AnyAsync(
                report => report.FarmId == farmId &&
                          report.ExistingPlantId == plantId &&
                          OpenStatuses.Contains(report.Status),
                cancellationToken);

    public Task<bool> HasOpenReportAtPoleAsync(
        Guid farmId,
        string poleLocationKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poleLocationKey);
        var normalizedKey = poleLocationKey.Trim();

        return context.PlantInventoryChangeReports
            .AsNoTracking()
            .AnyAsync(
                report => report.FarmId == farmId &&
                          report.PoleLocationKey == normalizedKey &&
                          OpenStatuses.Contains(report.Status),
                cancellationToken);
    }

    public void Add(PlantInventoryChangeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        context.PlantInventoryChangeReports.Add(report);
    }

    public void Update(PlantInventoryChangeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        context.PlantInventoryChangeReports.Update(report);
    }
}
