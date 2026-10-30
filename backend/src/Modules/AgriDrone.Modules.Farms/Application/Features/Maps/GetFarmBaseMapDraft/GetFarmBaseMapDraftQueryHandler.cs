using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDraft;

internal sealed class GetFarmBaseMapDraftQueryHandler(
    FarmsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext)
    : IRequestHandler<GetFarmBaseMapDraftQuery, Result<FarmBaseMapDraftResponse>>
{
    public async Task<Result<FarmBaseMapDraftResponse>> Handle(
        GetFarmBaseMapDraftQuery query,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid)
        {
            return Result.Failure<FarmBaseMapDraftResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            query.FarmId,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<FarmBaseMapDraftResponse>(FarmError.NotFound());
        }

        var queryable = dbContext.FarmBaseMapVersions
            .AsNoTracking()
            .Where(m => m.FarmId == query.FarmId);

        if (query.DraftId.HasValue)
        {
            queryable = queryable.Where(m => m.Id == query.DraftId.Value);
        }
        else
        {
            queryable = queryable.Where(m => m.Status == FarmBaseMapStatus.Draft);

            if (query.SurveyOrderId.HasValue)
            {
                queryable = queryable.Where(m => m.SourceSurveyOrderId == query.SurveyOrderId.Value);
            }
        }

        var draft = query.DraftId.HasValue
            ? await queryable.SingleOrDefaultAsync(cancellationToken)
            : await queryable.OrderByDescending(m => m.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        if (draft is null)
        {
            return Result.Failure<FarmBaseMapDraftResponse>(
                AppError.NotFound("FarmBaseMapDraft.NotFound", "Farm base map draft not found."));
        }

        var batch = await dbContext.MappingCandidateBatches
            .AsNoTracking()
            .Include(b => b.Candidates)
            .SingleOrDefaultAsync(
                b => b.FarmBaseMapVersionId == draft.Id || (b.FarmId == query.FarmId && b.SurveyOrderId == draft.SourceSurveyOrderId),
                cancellationToken);

        var candidates = batch?.Candidates.ToList() ?? [];
        var candidateDtos = candidates.Select(c => new CandidateDto(
                    CandidateId: c.CandidateId,
                    SourceObservationId: c.SourceObservationId,
                    EvidenceMediaAssetIds: c.EvidenceMediaAssetIds,
                    Longitude: c.OriginalPosition.X,
                    Latitude: c.OriginalPosition.Y,
                    ProposedRowIndex: c.ProposedRowIndex,
                    ProposedColumnIndex: c.ProposedColumnIndex,
                    PositionConfidence: c.PositionConfidence,
                    BoundaryClassification: c.BoundaryClassification,
                    ReviewDecision: c.ReviewDecision.ToString(),
                    CorrectedLongitude: c.CorrectedPosition?.X,
                    CorrectedLatitude: c.CorrectedPosition?.Y,
                    CorrectedRowIndex: c.CorrectedRowIndex,
                    CorrectedColumnIndex: c.CorrectedColumnIndex,
                    ProposedMatchedPlantId: c.ProposedMatchedPlantId,
                    CorrectedMatchedPlantId: c.CorrectedMatchedPlantId,
                    ReviewReason: c.ReviewReason,
                    ReviewedAt: c.ReviewedAt,
                    TreeId: c.TreeId)).ToList();

        var total = candidates.Count;
        var accepted = candidates.Count(c => c.ReviewDecision is MappingCandidateReviewDecision.CreateNew or MappingCandidateReviewDecision.MatchExisting);
        var rejected = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Reject);
        var relocated = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Relocate);
        var replaced = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Replace);
        var pending = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending);

        var response = new FarmBaseMapDraftResponse(
            DraftId: draft.Id,
            FarmId: draft.FarmId,
            VersionNumber: draft.VersionNumber,
            Status: draft.Status.ToString(),
            SourceSurveyOrderId: draft.SourceSurveyOrderId,
            SourceMissionId: draft.SourceMissionId,
            FarmBoundaryVersionId: draft.FarmBoundaryVersionId,
            HandoffId: draft.HandoffId,
            BatchId: batch?.Id,
            BatchStatus: batch?.Status.ToString(),
            JobId: batch?.JobId,
            ModelVersion: batch?.ModelVersion,
            AlgorithmVersion: batch?.AlgorithmVersion,
            ProducedAt: batch?.ProducedAt,
            TotalCandidates: total,
            PendingCount: pending,
            AcceptedCount: accepted,
            RejectedCount: rejected,
            RelocatedCount: relocated,
            ReplacedCount: replaced,
            GridBearingDeg: batch?.GridBearingDeg ?? draft.GridBearingDeg,
            RowSpacingM: batch?.RowSpacingM ?? draft.RowSpacingM,
            PlantSpacingM: batch?.PlantSpacingM ?? draft.PlantSpacingM,
            Candidates: candidateDtos);

        return Result.Success(response);
    }
}
