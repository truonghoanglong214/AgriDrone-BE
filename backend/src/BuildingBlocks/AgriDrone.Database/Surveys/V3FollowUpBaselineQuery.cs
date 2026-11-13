using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

namespace AgriDrone.Database.Surveys;

internal sealed class V3FollowUpBaselineQuery(
    AgriDroneSchemaDbContext context,
    V3FollowUpBaselineEligibilityReader eligibilityReader)
    : IV3FollowUpBaselineQuery
{
    public Task<V3FollowUpBaselineEligibilityResult> GetEligibleAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        eligibilityReader.GetEligibleAsync(context, tenantId, farmId, cancellationToken);
}
