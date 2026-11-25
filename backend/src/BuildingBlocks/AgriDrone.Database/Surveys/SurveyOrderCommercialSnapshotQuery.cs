using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyOrderCommercialSnapshotQuery(AgriDroneSchemaDbContext db)
    : ISurveyOrderCommercialSnapshotQuery
{
    public async Task<PinnedSurveyOrderCommercialSnapshot?> GetByOrderIdAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await db.Set<SurveyOrderCommercialSnapshot>()
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.SurveyOrderId == surveyOrderId, cancellationToken);

        if (snapshot is null) return null;

        return new PinnedSurveyOrderCommercialSnapshot(
            snapshot.SurveyOrderId,
            snapshot.BusinessPolicyVersionId,
            snapshot.ServiceCode,
            snapshot.MapsPoles,
            snapshot.ScreensDisease,
            snapshot.PriceVersionId,
            snapshot.PricePerPole,
            snapshot.Currency);
    }
}
