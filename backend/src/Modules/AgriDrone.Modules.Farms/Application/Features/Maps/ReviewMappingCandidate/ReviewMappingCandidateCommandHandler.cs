using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ReviewMappingCandidate;

internal sealed class ReviewMappingCandidateCommandHandler(
    FarmsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ReviewMappingCandidateCommand, Result<CandidateReviewResponse>>
{
    public async Task<Result<CandidateReviewResponse>> Handle(
        ReviewMappingCandidateCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<CandidateReviewResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<CandidateReviewResponse>(FarmError.NotFound());
        }

        var batch = await dbContext.MappingCandidateBatches
            .SingleOrDefaultAsync(
                b => b.FarmBaseMapVersionId == command.DraftId && b.FarmId == command.FarmId,
                cancellationToken);

        if (batch is null)
        {
            return Result.Failure<CandidateReviewResponse>(
                AppError.NotFound("MappingCandidateBatch.NotFound", "Mapping candidate batch not found."));
        }

        var candidate = await dbContext.MappingCandidates
            .SingleOrDefaultAsync(
                c => c.BatchId == batch.Id && c.CandidateId == command.CandidateId,
                cancellationToken);

        if (candidate is null)
        {
            return Result.Failure<CandidateReviewResponse>(
                AppError.NotFound("MappingCandidate.NotFound", $"Mapping candidate {command.CandidateId} not found in this batch."));
        }

        var now = timeProvider.GetUtcNow();

        if (batch.Status == MappingCandidateBatchStatus.PendingReview)
        {
            batch.StartReview(actorId, now);
        }

        try
        {
            candidate.ApplyReview(
                decision: command.Decision,
                correctedPosition: command.CorrectedPosition,
                correctedRowIndex: command.CorrectedRowIndex,
                correctedColumnIndex: command.CorrectedColumnIndex,
                correctedMatchedPlantId: command.CorrectedMatchedPlantId,
                reviewerId: actorId,
                reason: command.Reason,
                reviewedAt: now);

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<CandidateReviewResponse>(
                AppError.Validation("MappingCandidate.ReviewFailed", ex.Message));
        }

        var response = new CandidateReviewResponse(
            CandidateId: candidate.CandidateId,
            ReviewDecision: candidate.ReviewDecision.ToString(),
            CorrectedLongitude: candidate.CorrectedPosition?.X,
            CorrectedLatitude: candidate.CorrectedPosition?.Y,
            CorrectedRowIndex: candidate.CorrectedRowIndex,
            CorrectedColumnIndex: candidate.CorrectedColumnIndex,
            CorrectedMatchedPlantId: candidate.CorrectedMatchedPlantId,
            Reason: candidate.ReviewReason,
            ReviewedAt: candidate.ReviewedAt ?? now);

        return Result.Success(response);
    }
}
