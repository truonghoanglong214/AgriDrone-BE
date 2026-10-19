using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

internal sealed class FarmOperationalContextQueries(FarmsDbContext context)
    : IFarmOperationalContextQueries
{
    public async Task<FarmOperationalContextDto?> GetAssignedFarmOperationalContextAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        var farm = await context.Farms
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.Id == farmId && f.DeletedAt == null)
            .Select(f => new
            {
                f.Id,
                f.TenantId,
                f.Code,
                f.Name,
                f.Address,
                Status = f.Status.ToString(),
                f.AreaHectares,
                f.Version
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (farm is null)
        {
            return null;
        }

        var approvedBoundary = await context.FarmBoundaries
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId &&
                        b.FarmId == farmId &&
                        b.Status == FarmBoundaryStatus.Approved &&
                        b.SupersededAt == null)
            .OrderByDescending(b => b.VersionNumber)
            .Select(b => new ApprovedBoundaryDto(
                b.Id,
                b.VersionNumber,
                b.Geometry,
                b.Source.ToString(),
                b.EvidenceReference,
                b.ReviewedBy,
                b.ReviewedAt,
                b.ReviewReason,
                b.Version))
            .FirstOrDefaultAsync(cancellationToken);

        var zones = await context.FarmZones
            .AsNoTracking()
            .Where(z => z.FarmId == farmId && z.DeletedAt == null)
            .OrderBy(z => z.Code)
            .Select(z => new OperationalZoneDto(
                z.Id,
                z.Code,
                z.Name,
                z.Status.ToString(),
                z.AreaHectares,
                z.Boundary,
                z.Version))
            .ToListAsync(cancellationToken);

        var publishedBaseMap = await context.FarmBaseMapVersions
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId &&
                        m.FarmId == farmId &&
                        m.Status == FarmBaseMapStatus.Published)
            .OrderByDescending(m => m.VersionNumber)
            .Select(m => new PublishedBaseMapSummaryDto(
                m.Id,
                m.VersionNumber,
                m.SourceSurveyOrderId,
                m.PublishedAt,
                m.PublishedBy,
                m.Version))
            .FirstOrDefaultAsync(cancellationToken);

        return new FarmOperationalContextDto(
            farm.Id,
            farm.TenantId,
            farm.Code,
            farm.Name,
            farm.Address,
            farm.Status,
            farm.AreaHectares,
            farm.Version,
            approvedBoundary,
            zones,
            publishedBaseMap);
    }
}
