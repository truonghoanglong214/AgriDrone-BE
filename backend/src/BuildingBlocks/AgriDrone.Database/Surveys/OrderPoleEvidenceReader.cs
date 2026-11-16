using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

public sealed class OrderPoleEvidenceReader(
    MappingPublicationDbContext dbContext)
    : IOrderPoleEvidenceReader
{
    public async Task<Result<OrderPoleEvidence>> ReadEvidenceForOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.SurveyOrders
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<OrderPoleEvidence>(
                AppError.NotFound("SurveyOrder.NotFound", "Survey order not found."));
        }

        if (order.FarmBoundaryVersionId is not Guid boundaryId)
        {
            return Result.Failure<OrderPoleEvidence>(
                AppError.Conflict("SurveyOrder.MissingBoundary", "Cannot verify billable poles without an approved farm boundary."));
        }

        var baseMapId = order.FarmBaseMapVersionId ?? Guid.Empty;

        var boundary = await dbContext.FarmBoundaries
            .SingleOrDefaultAsync(b => b.Id == boundaryId, cancellationToken);

        if (boundary is null)
        {
            return Result.Failure<OrderPoleEvidence>(
                AppError.NotFound("FarmBoundary.NotFound", "Farm boundary was not found."));
        }

        var hasUnresolvedBoundaryExceptions = await dbContext.BoundaryExceptions
            .AnyAsync(e => e.FarmBoundaryVersionId == boundaryId && e.State != BoundaryExceptionState.Resolved, cancellationToken);

        if (hasUnresolvedBoundaryExceptions)
        {
            return Result.Failure<OrderPoleEvidence>(
                AppError.Conflict("BoundaryException.Unresolved", "There are unresolved boundary exceptions for this farm boundary."));
        }

        var batch = await dbContext.MappingCandidateBatches
            .Include(b => b.Candidates)
            .SingleOrDefaultAsync(b => b.SurveyOrderId == orderId && b.FarmBaseMapVersionId == baseMapId, cancellationToken)
            ?? await dbContext.MappingCandidateBatches
                .Include(b => b.Candidates)
                .Where(b => b.SurveyOrderId == orderId)
                .OrderByDescending(b => b.ProducedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (batch is null)
        {
            return Result.Failure<OrderPoleEvidence>(
                AppError.NotFound("MappingCandidateBatch.NotFound", "Mapping candidate batch not found for this survey order."));
        }

        if (batch.Candidates.Any(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending))
        {
            return Result.Failure<OrderPoleEvidence>(
                AppError.Conflict("MappingCandidate.PendingReview", "Mapping candidates must be reviewed before verifying pole count."));
        }

        var effectivePolicy = await dbContext.SpatialPolicies
            .Where(p => p.StreamKey == "DEFAULT" &&
                        p.EffectiveFrom <= batch.ProducedAt &&
                        (p.EffectiveTo == null || p.EffectiveTo > batch.ProducedAt))
            .OrderByDescending(p => p.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (effectivePolicy is null)
        {
            return Result.Failure<OrderPoleEvidence>(
                AppError.Conflict("SpatialPolicy.NotFound", "No effective spatial policy exists for the survey capture time."));
        }

        var billableCount = 0;
        if (baseMapId != Guid.Empty)
        {
            var plantsInBaseMap = await dbContext.Plants
                .Where(p => p.FarmBaseMapVersionId == baseMapId &&
                            p.FarmId == order.FarmId &&
                            p.LifecycleStatus == PlantLifecycleStatus.Active &&
                            p.PlantProfileId != null)
                .ToListAsync(cancellationToken);

            billableCount = plantsInBaseMap
                .Where(p => p.Location != null && boundary.Geometry.Contains(p.Location))
                .Select(p => p.PlantProfileId!.Value)
                .Distinct()
                .Count();
        }

        if (billableCount == 0)
        {
            billableCount = batch.Candidates
                .Where(c => c.ReviewDecision is MappingCandidateReviewDecision.CreateNew
                                             or MappingCandidateReviewDecision.MatchExisting
                                             or MappingCandidateReviewDecision.Relocate
                                             or MappingCandidateReviewDecision.Replace)
                .Count(c => boundary.Geometry.Contains(c.CorrectedPosition ?? c.OriginalPosition));
        }

        return Result.Success(new OrderPoleEvidence(
            SurveyOrderId: order.Id,
            TenantId: order.TenantId,
            FarmId: order.FarmId,
            MissionId: batch.MissionId,
            CaptureAttemptId: null,
            FarmBaseMapVersionId: baseMapId != Guid.Empty ? baseMapId : (batch.FarmBaseMapVersionId ?? Guid.Empty),
            FarmBoundaryVersionId: boundaryId,
            MappingCandidateBatchId: batch.Id,
            SpatialPolicyVersionId: effectivePolicy.Id,
            BillablePoleCount: billableCount));
    }
}
