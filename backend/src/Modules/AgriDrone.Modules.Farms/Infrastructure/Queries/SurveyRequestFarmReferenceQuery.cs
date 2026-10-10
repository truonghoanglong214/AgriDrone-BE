using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

internal sealed class SurveyRequestFarmReferenceQuery(
    FarmsDbContext context) : ISurveyRequestFarmReferenceQuery
{
    public Task<SurveyRequestFarmReference?> GetAsync(
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(farmId, Guid.Empty);

        return context.Farms
            .AsNoTracking()
            .Where(farm => farm.Id == farmId)
            .Select(farm => new SurveyRequestFarmReference(
                farm.TenantId,
                farm.Id,
                farm.Status == GeneralStatus.Active &&
                    farm.DeletedAt == null,
                farm.Name,
                farm.Address,
                farm.AreaHectares,
                farm.CenterPoint == null
                    ? null
                    : farm.CenterPoint.X,
                farm.CenterPoint == null
                    ? null
                    : farm.CenterPoint.Y,
                farm.CenterPoint == null
                    ? null
                    : farm.CenterPoint.SRID))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
