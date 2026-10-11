using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderBoundaryQueries(
    SurveyBoundaryVerificationDbContext context)
    : ISurveyOrderBoundaryQueries
{
    public async Task<SurveyOrderBoundaryScopeSnapshot?> GetAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await context.SurveyOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == surveyOrderId,
                cancellationToken);
        if (order is null)
        {
            return null;
        }

        var boundary = await context.FarmBoundaries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == order.TenantId &&
                        item.FarmId == order.FarmId &&
                        item.Status == FarmBoundaryStatus.Approved,
                cancellationToken);
        var zones = await context.FarmZones
            .AsNoTracking()
            .Where(zone => zone.FarmId == order.FarmId &&
                           zone.DeletedAt == null)
            .OrderBy(zone => zone.Code)
            .ThenBy(zone => zone.Id)
            .Select(zone => new SurveyOrderZoneScopeSnapshot(
                zone.Id,
                zone.Code,
                zone.Name,
                zone.Boundary,
                zone.Status.ToString(),
                zone.Version))
            .ToArrayAsync(cancellationToken);

        return new SurveyOrderBoundaryScopeSnapshot(
            order.Id,
            order.OrderNumber,
            order.TenantId,
            order.FarmId,
            order.Status,
            order.Version,
            order.RequiresBaselineMapping,
            boundary is null
                ? null
                : new SurveyOrderBoundarySnapshot(
                    boundary.Id,
                    boundary.VersionNumber,
                    boundary.Version,
                    boundary.Geometry,
                    boundary.Source switch
                    {
                        FarmBoundarySource.Applicant => "APPLICANT",
                        FarmBoundarySource.TenantOwner => "TENANT_OWNER",
                        FarmBoundarySource.SystemManager => "SYSTEM_MANAGER",
                        FarmBoundarySource.LegacyImport => "LEGACY_IMPORT",
                        _ => throw new InvalidOperationException(
                            "Unsupported FarmBoundary source.")
                    },
                    boundary.EvidenceReference,
                    boundary.ReviewedBy,
                    boundary.ReviewedAt),
            zones);
    }
}
