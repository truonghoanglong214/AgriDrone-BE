using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Surveys.Application.Abstractions.Results;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Ports;

internal sealed class SurveyComboPendingReadPort(SurveysDbContext db) : ISurveyComboPendingReadPort
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<SurveyComboPendingReviewBatch>> GetPendingBatchForReviewAsync(
        Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await db.SurveyOrders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return Result.Failure<SurveyComboPendingReviewBatch>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

        return await ResolvePendingBatchForOrderAsync(order.TenantId, order.FarmId, order.Id, cancellationToken);
    }

    public Task<Result<SurveyComboPendingReviewBatch>> GetPendingBatchForReviewAsync(
        Guid tenantId,
        Guid farmId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || farmId == Guid.Empty || orderId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure<SurveyComboPendingReviewBatch>(
                AppError.Validation("Scope.Required", "TenantId, FarmId, and OrderId are required.")));
        }

        return ResolvePendingBatchForOrderAsync(tenantId, farmId, orderId, cancellationToken);
    }

    public async Task<Result<SurveyComboPendingReviewBatch>> GetBatchByIdAsync(
        Guid orderId, Guid batchId, CancellationToken cancellationToken = default)
    {
        var order = await db.SurveyOrders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return Result.Failure<SurveyComboPendingReviewBatch>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

        var batch = await db.SurveyComboImportBatches.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == batchId &&
                                       b.SurveyOrderId == orderId &&
                                       b.TenantId == order.TenantId &&
                                       b.FarmId == order.FarmId, cancellationToken);

        if (batch is null)
            return Result.Failure<SurveyComboPendingReviewBatch>(AppError.NotFound("ComboBatch.NotFound", "COMBO batch was not found for this order context."));

        return await MapWithPlantIdsAsync(batch, cancellationToken);
    }

    public async Task<Result<SurveyComboReviewReadContext>> GetComboReviewContextAsync(
        Guid tenantId,
        Guid farmId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await db.SurveyOrders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId && o.FarmId == farmId, cancellationToken);

        if (order is null)
            return Result.Failure<SurveyComboReviewReadContext>(AppError.NotFound("Order.NotFound", "Survey order was not found in tenant/farm scope."));

        var batchResult = await ResolvePendingBatchForOrderAsync(tenantId, farmId, orderId, cancellationToken);
        if (batchResult.IsFailure)
            return Result.Failure<SurveyComboReviewReadContext>(batchResult.Error);

        var batch = batchResult.Value;

        if (!order.VerifiedPoleCountSnapshotId.HasValue)
        {
            return Result.Failure<SurveyComboReviewReadContext>(
                AppError.Conflict("CountSnapshot.Missing", "Survey order does not have a verified pole count snapshot pinned."));
        }

        var snapshot = await db.SurveyOrderPoleCountSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s =>
                s.Id == order.VerifiedPoleCountSnapshotId.Value &&
                s.SurveyOrderId == orderId &&
                s.TenantId == tenantId &&
                s.FarmId == farmId &&
                s.CaptureAttemptId == batch.CaptureAttemptId,
                cancellationToken);

        if (snapshot is null)
        {
            return Result.Failure<SurveyComboReviewReadContext>(
                AppError.Conflict("CountSnapshot.ContextMismatch", "Verified pole count snapshot does not match order or active capture attempt."));
        }

        var reviewHeads = await db.SurveyComboReviewRevisions.AsNoTracking()
            .Where(r => r.TenantId == tenantId &&
                        r.FarmId == farmId &&
                        r.SurveyOrderId == orderId &&
                        r.ComboBatchId == batch.BatchId &&
                        r.IsCurrent)
            .ToListAsync(cancellationToken);

        if (reviewHeads.Count > 1)
        {
            return Result.Failure<SurveyComboReviewReadContext>(
                AppError.Conflict("ComboReview.AmbiguousHead", "Multiple current COMBO review revisions detected for the same batch."));
        }

        SurveyComboReviewSummaryDto? reviewSummary = null;
        if (reviewHeads.Count == 1)
        {
            var head = reviewHeads[0];
            var decisionsResult = head.ReadDecisions();
            if (decisionsResult.IsFailure)
                return Result.Failure<SurveyComboReviewReadContext>(decisionsResult.Error);

            var decisions = decisionsResult.Value;
            reviewSummary = new SurveyComboReviewSummaryDto(
                ReviewRevisionId: head.Id,
                RevisionNumber: head.RevisionNumber,
                PredecessorReviewId: head.PredecessorReviewId,
                Outcome: head.Outcome,
                ReviewReason: head.ReviewReason,
                ConfirmNoDiseaseObserved: head.ConfirmNoDiseaseObserved,
                NegativeResultConfirmationEvidence: head.NegativeResultConfirmationEvidence,
                ReviewedBy: head.ReviewedBy,
                ReviewedAt: head.ReviewedAt,
                DecisionFingerprint: head.DecisionFingerprint,
                FindingsCount: head.FindingsCount,
                AcceptedFindingsCount: head.AcceptedFindingsCount,
                ExcludedFindingsCount: head.ExcludedFindingsCount,
                ZonesCount: head.ZonesCount,
                ApprovedZonesCount: head.ApprovedZonesCount,
                RejectedZonesCount: head.RejectedZonesCount,
                BlockersCount: head.BlockersCount,
                ResolvedBlockersCount: head.ResolvedBlockersCount,
                UnresolvedBlockersCount: head.UnresolvedBlockersCount,
                FindingDecisions: decisions.FindingDecisions,
                ZoneDecisions: decisions.ZoneDecisions,
                BlockerResolutions: decisions.BlockerResolutions);
        }

        var expectedTokens = new SurveyExpectedFreezeTokensDto(
            OrderId: order.Id,
            ExpectedOrderVersion: order.Version,
            ExpectedAttemptId: batch.CaptureAttemptId,
            ExpectedCountSnapshotId: snapshot.Id,
            ExpectedCountFingerprint: snapshot.EvidenceFingerprint,
            ExpectedComboBatchId: batch.BatchId,
            ExpectedComboSourceRevision: batch.SourceRevision,
            ExpectedComboBatchFingerprint: batch.CanonicalFingerprint,
            ExpectedComboReviewRevisionId: reviewSummary?.ReviewRevisionId,
            ExpectedComboReviewFingerprint: reviewSummary?.DecisionFingerprint);

        return Result.Success(new SurveyComboReviewReadContext(
            Batch: batch,
            VerifiedPoleCountSnapshotId: snapshot.Id,
            VerifiedBillablePoleCount: snapshot.VerifiedBillablePoleCount,
            CountSnapshotFingerprint: snapshot.EvidenceFingerprint,
            CountSnapshotRevision: snapshot.RevisionNumber,
            CurrentReview: reviewSummary,
            ExpectedTokens: expectedTokens));
    }

    private async Task<Result<SurveyComboPendingReviewBatch>> ResolvePendingBatchForOrderAsync(
        Guid tenantId,
        Guid farmId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        // V06/V11: Resolve active capture attempt without using pointer column, matching uq_capture_attempts_active_order filter
        var activeAttempt = await db.CaptureAttempts.AsNoTracking()
            .Where(a => a.OrderId == orderId &&
                        a.TenantId == tenantId &&
                        a.FarmId == farmId &&
                        (a.Status == CaptureAttemptStatus.Planned ||
                         a.Status == CaptureAttemptStatus.PreflightReady ||
                         a.Status == CaptureAttemptStatus.InFlight ||
                         a.Status == CaptureAttemptStatus.AwaitingUpload ||
                         a.Status == CaptureAttemptStatus.Uploaded ||
                         a.Status == CaptureAttemptStatus.Processing))
            .SingleOrDefaultAsync(cancellationToken);

        if (activeAttempt is null)
            return Result.Failure<SurveyComboPendingReviewBatch>(AppError.NotFound("ComboBatch.NoActiveAttempt", "Survey order has no active capture attempt."));

        var currentAttemptId = activeAttempt.Id;

        var pendingBatches = await db.SurveyComboImportBatches.AsNoTracking()
            .Where(b => b.TenantId == tenantId &&
                        b.FarmId == farmId &&
                        b.SurveyOrderId == orderId &&
                        b.CaptureAttemptId == currentAttemptId &&
                        b.Status == SurveyComboImportBatch.StatusPendingReview)
            .ToListAsync(cancellationToken);

        if (pendingBatches.Count == 0)
            return Result.Failure<SurveyComboPendingReviewBatch>(AppError.NotFound("ComboBatch.NotFound", "No pending COMBO batch found for the active capture attempt of this order."));

        // Determine head: a batch whose Id is not referenced as PredecessorBatchId by any other pending batch
        var predecessorIds = pendingBatches
            .Where(b => b.PredecessorBatchId.HasValue)
            .Select(b => b.PredecessorBatchId!.Value)
            .ToHashSet();

        var headCandidates = pendingBatches
            .Where(b => !predecessorIds.Contains(b.Id))
            .ToList();

        if (headCandidates.Count != 1)
        {
            return Result.Failure<SurveyComboPendingReviewBatch>(
                AppError.Conflict("ComboBatch.AmbiguousHead", "Multiple or ambiguous pending COMBO lineage heads detected."));
        }

        return await MapWithPlantIdsAsync(headCandidates[0], cancellationToken);
    }

    private sealed record PlantByProfileRow(Guid PlantProfileId, Guid PlantId);

    private async Task<Dictionary<Guid, Guid>> LoadPlantIdsByProfileAsync(
        Guid farmId,
        ComboAnalysisReadyV1 payload,
        CancellationToken cancellationToken)
    {
        var profileIds = payload.Findings
            .Where(f => f.PlantProfileId.HasValue)
            .Select(f => f.PlantProfileId!.Value)
            .Distinct()
            .ToArray();
        if (profileIds.Length == 0)
        {
            return [];
        }

        var rows = await db.Database.SqlQuery<PlantByProfileRow>($"""
            SELECT p.plant_profile_id AS "PlantProfileId", p.id AS "PlantId"
            FROM plant.plants p
            WHERE p.farm_id = {farmId}
              AND p.plant_profile_id = ANY({profileIds})
              AND p.lifecycle_status = 'ACTIVE'
            ORDER BY p.created_at DESC, p.id
            """).ToListAsync(cancellationToken);

        var lookup = new Dictionary<Guid, Guid>();
        foreach (var row in rows)
        {
            lookup.TryAdd(row.PlantProfileId, row.PlantId);
        }
        return lookup;
    }

    private async Task<Result<SurveyComboPendingReviewBatch>> MapWithPlantIdsAsync(
        SurveyComboImportBatch batch,
        CancellationToken cancellationToken)
    {
        var mapped = Map(batch);
        if (mapped.IsFailure)
        {
            return mapped;
        }

        ComboAnalysisReadyV1? payload;
        try
        {
            payload = batch.RawPayload.Deserialize<ComboAnalysisReadyV1>(PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return mapped;
        }

        if (payload is null)
        {
            return mapped;
        }

        var plantIdsByProfile = await LoadPlantIdsByProfileAsync(batch.FarmId, payload, cancellationToken);
        if (plantIdsByProfile.Count == 0)
        {
            return mapped;
        }

        var findings = mapped.Value.Findings
            .Select(f => f.PlantProfileId is Guid profileId && plantIdsByProfile.TryGetValue(profileId, out var plantId)
                ? f with { PlantId = plantId }
                : f)
            .ToList();

        return Result.Success(mapped.Value with { Findings = findings });
    }

    private static Result<SurveyComboPendingReviewBatch> Map(SurveyComboImportBatch batch)
    {
        ComboAnalysisReadyV1? payload;
        try
        {
            payload = batch.RawPayload.Deserialize<ComboAnalysisReadyV1>(PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return Result.Failure<SurveyComboPendingReviewBatch>(
                AppError.Validation("ComboBatch.CorruptedPayload", "Raw payload cannot be deserialized into COMBO model."));
        }

        if (payload is null)
        {
            return Result.Failure<SurveyComboPendingReviewBatch>(
                AppError.Validation("ComboBatch.NullPayload", "Raw payload deserialized to null."));
        }

        var findings = payload.Findings.Select(f => new ComboPendingFindingDto(
            FindingId: f.FindingId,
            FindingVersion: f.FindingVersion,
            PlantProfileId: f.PlantProfileId,
            MappingCandidateId: f.MappingCandidateId,
            Longitude: f.Position.Coordinates[0],
            Latitude: f.Position.Coordinates[1],
            ObservedAt: f.ObservedAt,
            SuspectedClassCode: f.SuspectedClassCode,
            ClassificationNature: f.ClassificationNature,
            Confidence: f.Confidence,
            Uncertainty: f.Uncertainty,
            Limitations: f.Limitations,
            EvidenceMediaIds: f.EvidenceMedia.Select(m => m.MediaAssetId).ToList()
        )).ToList();

        var exceptions = payload.BoundaryExceptions.Select(e => new ComboPendingBoundaryExceptionDto(
            SourceReferenceId: e.SourceReferenceId,
            Longitude: e.OriginalPosition.Coordinates[0],
            Latitude: e.OriginalPosition.Coordinates[1],
            BoundaryClassification: e.BoundaryClassification,
            MeasuredBoundaryDistanceM: e.MeasuredBoundaryDistanceM,
            BoundaryThresholdM: e.BoundaryThresholdM,
            EvidenceMediaIds: e.EvidenceMedia.Select(m => m.MediaAssetId).ToList()
        )).ToList();

        var zones = payload.DiseaseZones.Select(z => new ComboPendingDiseaseZoneDto(
            SourceProposalId: z.SourceProposalId,
            Geometry: z.Geometry,
            SuspectedClassCode: z.SuspectedClassCode,
            MembershipVersion: z.MembershipVersion,
            Memberships: z.Memberships.Select(m => new ComboPendingZoneMembershipDto(
                FindingId: m.FindingId,
                PlantProfileId: m.PlantProfileId,
                MappingCandidateId: m.MappingCandidateId,
                Confidence: m.Confidence
            )).ToList(),
            GuidanceCandidates: z.GuidanceCandidates.Select(g => new ComboPendingGuidanceCandidateDto(
                GuidanceId: g.GuidanceId,
                VersionNumber: g.VersionNumber,
                Code: g.Code,
                SourceReference: g.SourceReference
            )).ToList(),
            EvidenceMediaIds: z.EvidenceMedia.Select(m => m.MediaAssetId).ToList()
        )).ToList();

        if (batch.BlockingItems == null || batch.BlockingItems.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Result.Failure<SurveyComboPendingReviewBatch>(
                AppError.Validation("ComboBatch.CorruptedBlockingItems", "Persisted blocking items are missing or corrupted."));
        }

        List<ComboPendingBlockingItem>? persistedItems;
        try
        {
            persistedItems = batch.BlockingItems.Deserialize<List<ComboPendingBlockingItem>>(PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return Result.Failure<SurveyComboPendingReviewBatch>(
                AppError.Validation("ComboBatch.CorruptedBlockingItems", "Persisted blocking items JSON cannot be deserialized."));
        }

        if (persistedItems is null)
        {
            return Result.Failure<SurveyComboPendingReviewBatch>(
                AppError.Validation("ComboBatch.CorruptedBlockingItems", "Persisted blocking items deserialized to null."));
        }

        if (persistedItems.Count != batch.BlockingCount)
        {
            return Result.Failure<SurveyComboPendingReviewBatch>(
                AppError.Conflict("ComboBatch.BlockingCountMismatch",
                    $"Persisted blocking item count {persistedItems.Count} does not match recorded BlockingCount {batch.BlockingCount}."));
        }

        foreach (var item in persistedItems)
        {
            if (string.IsNullOrWhiteSpace(item.SourceType) ||
                string.IsNullOrWhiteSpace(item.SourceId) ||
                string.IsNullOrWhiteSpace(item.ReasonCode))
            {
                return Result.Failure<SurveyComboPendingReviewBatch>(
                    AppError.Validation("ComboBatch.InvalidBlockingItem", "Every persisted blocking item must have non-empty SourceType, SourceId and ReasonCode."));
            }
        }

        var blockingReasons = persistedItems.Select(i => $"{i.SourceType}:{i.SourceId}:{i.ReasonCode}").ToList();

        return Result.Success(new SurveyComboPendingReviewBatch(
            BatchId: batch.Id,
            TenantId: batch.TenantId,
            FarmId: batch.FarmId,
            SurveyOrderId: batch.SurveyOrderId,
            CaptureAttemptId: batch.CaptureAttemptId,
            MissionId: batch.MissionId,
            JobId: batch.JobId,
            SourceRevision: batch.SourceRevision,
            HandoffId: batch.HandoffId,
            PredecessorBatchId: batch.PredecessorBatchId,
            SupersedesHandoffId: batch.SupersedesHandoffId,
            BusinessPolicyVersionId: batch.BusinessPolicyVersionId,
            MappingKind: batch.MappingKind,
            MappingCandidateBatchId: batch.MappingCandidateBatchId,
            FarmBaseMapVersionId: batch.FarmBaseMapVersionId,
            BaselineMapVersionId: batch.BaselineMapVersionId,
            FarmBoundaryVersionId: batch.FarmBoundaryVersionId,
            CanonicalFingerprint: batch.CanonicalFingerprint,
            ProducedAt: batch.ProducedAt,
            ImportedAt: batch.ImportedAt,
            BlockingCount: batch.BlockingCount,
            BlockingReasons: blockingReasons,
            Status: batch.Status,
            ResultNature: payload.ResultNature,
            LimitationNotice: payload.LimitationNotice,
            Findings: findings,
            BoundaryExceptions: exceptions,
            DiseaseZones: zones));
    }
}
