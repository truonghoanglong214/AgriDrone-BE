using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

/// <summary>One eligibility definition for intake and transactional approval.</summary>
internal sealed class V3FollowUpBaselineEligibilityReader(
    IV3BaselineDeliveryProofReader deliveryProofReader)
{
    internal async Task<V3FollowUpBaselineEligibilityResult> GetEligibleAsync(
        DbContext context,
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(farmId, Guid.Empty);
        cancellationToken.ThrowIfCancellationRequested();

        var approvalContext = context as SurveyApprovalDbContext;
        approvalContext?.EnsureApprovalTransaction();

        var proof = await deliveryProofReader.ReadCurrentAsync(
            context, tenantId, farmId, cancellationToken);

        if (proof is null)
        {
            return V3FollowUpBaselineEligibilityResult.Ineligible(
                "Durable V3 baseline release evidence is not available; follow-up is not ready.");
        }

        if (!proof.AllowsFollowUp(tenantId, farmId))
        {
            return V3FollowUpBaselineEligibilityResult.Ineligible(
                "Baseline release evidence is missing, unsettled, withdrawn or belongs to another farm.");
        }

        if (approvalContext is not null)
        {
            var exists = await SurveyApprovalRowLock.ExistsAsync(
                approvalContext,
                """
                SELECT base_map.id
                FROM farm.farm_base_map_versions AS base_map
                JOIN survey.survey_orders AS source_order
                  ON source_order.id = base_map.source_survey_order_id
                 AND source_order.tenant_id = base_map.tenant_id
                 AND source_order.farm_id = base_map.farm_id
                WHERE base_map.id = @base_map_id
                  AND source_order.id = @source_order_id
                  AND base_map.tenant_id = @tenant_id
                  AND base_map.farm_id = @farm_id
                  AND base_map.status IN ('PUBLISHED', 'SUPERSEDED')
                  AND source_order.scope = 'V3'::system.contract_scope
                  AND source_order.status = 'COMPLETED'::system.survey_order_status
                FOR SHARE OF base_map, source_order
                """,
                cancellationToken,
                SurveyApprovalRowLock.Id("base_map_id", proof.BaseMapVersionId),
                SurveyApprovalRowLock.Id("source_order_id", proof.SourceOrderId),
                SurveyApprovalRowLock.Id("tenant_id", tenantId),
                SurveyApprovalRowLock.Id("farm_id", farmId));

            if (!exists)
            {
                return V3FollowUpBaselineEligibilityResult.Ineligible(
                    "The released baseline is no longer current or its source order is unavailable.");
            }
        }

        var candidate = await (
            from map in context.Set<FarmBaseMapVersion>().AsNoTracking()
            join order in context.Set<SurveyOrder>().AsNoTracking()
                on new { Id = map.SourceSurveyOrderId, map.TenantId, map.FarmId }
                equals new { order.Id, order.TenantId, order.FarmId }
            where map.Id == proof.BaseMapVersionId &&
                  order.Id == proof.SourceOrderId &&
                  map.TenantId == tenantId && map.FarmId == farmId &&
                  (map.Status == FarmBaseMapStatus.Published || map.Status == FarmBaseMapStatus.Superseded) &&
                  order.Scope == ContractScope.V3 &&
                  order.Status == SurveyOrderStatus.Completed
            select new { map.Id, SourceOrderId = order.Id, order.ConfirmedSurveyPoleCount, order.VerifiedBillablePoleCount })
            .SingleOrDefaultAsync(cancellationToken);

        var count = candidate?.VerifiedBillablePoleCount ?? candidate?.ConfirmedSurveyPoleCount?.Value;
        if (candidate is null || count is null or <= 0)
        {
            return V3FollowUpBaselineEligibilityResult.Ineligible(
                "The released baseline is not current or has no valid confirmed pole count.");
        }

        var current = await (
            from map in context.Set<FarmBaseMapVersion>().AsNoTracking()
            join order in context.Set<SurveyOrder>().AsNoTracking() on map.SourceSurveyOrderId equals order.Id
            where map.TenantId == tenantId && map.FarmId == farmId &&
                  (map.Id == proof.BaseMapVersionId || map.BaselineMapVersionId == proof.BaseMapVersionId) &&
                  order.Scope == ContractScope.V3 && (map.Status == FarmBaseMapStatus.Published ||
                    map.Status == FarmBaseMapStatus.InternalApproved || map.Id == proof.BaseMapVersionId)
            orderby map.VersionNumber descending
            select new { map.Id, map.Status, SourceOrderId = order.Id, OrderStatus = order.Status, order.VerifiedBillablePoleCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null || current.Status != FarmBaseMapStatus.Published ||
            current.OrderStatus != SurveyOrderStatus.Completed)
            return V3FollowUpBaselineEligibilityResult.Ineligible(
                "The latest accepted snapshot has not been released.");

        var currentProof = await deliveryProofReader.ReadCurrentAsync(context, tenantId, farmId, cancellationToken);
        if (currentProof?.ReleaseId != proof.ReleaseId || !currentProof.AllowsFollowUp(tenantId, farmId))
            return V3FollowUpBaselineEligibilityResult.Ineligible("The baseline release is no longer available.");

        return V3FollowUpBaselineEligibilityResult.Eligible(
            current.Id, current.SourceOrderId, current.VerifiedBillablePoleCount ?? count.Value, proof.BaseMapVersionId);
    }
}
