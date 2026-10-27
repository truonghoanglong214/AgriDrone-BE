using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ReviewMappingCandidatesBatch;

internal sealed class ReviewMappingCandidatesBatchCommandHandler(
    FarmsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ReviewMappingCandidatesBatchCommand, Result<BatchReviewSummaryResponse>>
{
    public async Task<Result<BatchReviewSummaryResponse>> Handle(
        ReviewMappingCandidatesBatchCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<BatchReviewSummaryResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<BatchReviewSummaryResponse>(FarmError.NotFound());
        }

        var batch = await dbContext.MappingCandidateBatches
            .Include(b => b.Candidates)
            .SingleOrDefaultAsync(
                b => b.FarmBaseMapVersionId == command.DraftId && b.FarmId == command.FarmId,
                cancellationToken);

        if (batch is null)
        {
            return Result.Failure<BatchReviewSummaryResponse>(
                AppError.NotFound("MappingCandidateBatch.NotFound", "Mapping candidate batch not found."));
        }

        var now = timeProvider.GetUtcNow();
        if (batch.Status == MappingCandidateBatchStatus.PendingReview)
        {
            batch.StartReview(actorId, now);
        }

        var candidatesById = batch.Candidates.ToDictionary(c => c.CandidateId);
        var reviewedCount = 0;

        foreach (var reviewItem in command.Reviews)
        {
            if (candidatesById.TryGetValue(reviewItem.CandidateId, out var candidate))
            {
                var reason = !string.IsNullOrWhiteSpace(reviewItem.Reason)
                    ? reviewItem.Reason
                    : command.BatchReason;

                candidate.ApplyReview(
                    decision: reviewItem.Decision,
                    correctedPosition: reviewItem.CorrectedPosition,
                    correctedRowIndex: reviewItem.CorrectedRowIndex,
                    correctedColumnIndex: reviewItem.CorrectedColumnIndex,
                    correctedMatchedPlantId: reviewItem.CorrectedMatchedPlantId,
                    reviewerId: actorId,
                    reason: reason,
                    reviewedAt: now);

                reviewedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var totalReviewed = reviewedCount;
        var accepted = batch.Candidates.Count(c => c.ReviewDecision is MappingCandidateReviewDecision.Accept or MappingCandidateReviewDecision.CreateNew);
        var rejected = batch.Candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Reject);
        var relocated = batch.Candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Relocate);
        var replaced = batch.Candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Replace);
        var remainingPending = batch.Candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending);

        return Result.Success(new BatchReviewSummaryResponse(
            TotalReviewed: totalReviewed,
            AcceptedCount: accepted,
            RejectedCount: rejected,
            RelocatedCount: relocated,
            ReplacedCount: replaced,
            RemainingPendingCount: remainingPending));
    }
}
