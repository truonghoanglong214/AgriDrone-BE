using AgriDrone.Modules.Surveys.Application.Features.Reports;
using Microsoft.Extensions.Caching.Memory;

namespace AgriDrone.Api.Reporting;

public sealed class RevenueReportSnapshots(TimeProvider clock) : IDisposable
{
    private const int Capacity = 128;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private readonly MemoryCache snapshots = new(new MemoryCacheOptions { SizeLimit = Capacity });
    private readonly object gate = new();

    public MonthlyFinancialReport Save(Guid actorId, MonthlyFinancialReport report)
    {
        var snapshotId = Guid.NewGuid();
        var expiresAt = clock.GetUtcNow() + Lifetime;
        var snapshot = report with { SnapshotId = snapshotId, SnapshotExpiresAt = expiresAt };
        lock (gate)
        {
            if (snapshots.Count >= Capacity) snapshots.Compact(0.10);
            snapshots.Set((actorId, snapshotId), snapshot, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Lifetime,
                Size = 1
            });
        }
        return snapshot;
    }

    public MonthlyFinancialReport? Find(Guid actorId, Guid snapshotId) =>
        snapshots.TryGetValue<MonthlyFinancialReport>((actorId, snapshotId), out var report) &&
        report?.SnapshotExpiresAt > clock.GetUtcNow() ? report : null;

    public void Dispose() => snapshots.Dispose();
}
