using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.Modules.Surveys.Application.Abstractions.Results;
using AgriDrone.Modules.Surveys.Application.Features.Results.FreezeSurveyInternalResult;
using AgriDrone.Modules.Surveys.Application.Features.Results.GetSurveyResultReview;
using AgriDrone.Modules.Surveys.Application.Features.Results.RecordSurveyComboReview;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyResultReviewTransaction(
    AgriDroneSchemaDbContext db,
    IExecutionContext executionContext,
    TimeProvider timeProvider) : ISurveyResultReviewTransaction
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    private sealed class Fault(AppError error) : Exception(error.Description)
    {
        public AppError Error { get; } = error;
    }

    private sealed record OrderScopeRow(Guid Id, Guid TenantId, Guid FarmId, Guid? PreviousCompatibleOrderId);

    public async Task<Result<SurveyResultReviewResponse>> GetReviewAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
            return Result.Failure<SurveyResultReviewResponse>(AppError.Validation("OrderId.Required", "OrderId is required."));
        if (actorId == Guid.Empty)
            return Result.Failure<SurveyResultReviewResponse>(AppError.Unauthorized("SurveyResultReview.Unauthorized", "Valid actor is required."));

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            var scope = await db.Set<SurveyOrder>().AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => new OrderScopeRow(o.Id, o.TenantId, o.FarmId, o.PreviousCompatibleOrderId))
                .SingleOrDefaultAsync(cancellationToken);

            if (scope is null)
                return Result.Failure<SurveyResultReviewResponse>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

            // ADR 0020 Rank 1: Authorize assigned Manager BEFORE reading evidence
            if (!await IsAssignedManagerAsync(scope.TenantId, scope.FarmId, actorId, cancellationToken))
                return Result.Failure<SurveyResultReviewResponse>(AppError.Forbidden("SurveyResultReview.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            var order = await db.Set<SurveyOrder>().AsNoTracking()
                .SingleAsync(o => o.Id == orderId, cancellationToken);

            if (order.Scope != ContractScope.V3)
                return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("SurveyOrder.ScopeInvalid", "Only V3 orders are supported by this review endpoint."));

            var commercial = await db.Set<SurveyOrderCommercialSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(c => c.SurveyOrderId == order.Id, cancellationToken);
            if (commercial is null)
                return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("CommercialSnapshot.Missing", "Pinned commercial snapshot was not found."));

            SurveyInternalApprovedResultRevision? currentApproval = null;
            if (order.CurrentApprovedResultRevisionId.HasValue)
            {
                currentApproval = await db.Set<SurveyInternalApprovedResultRevision>().AsNoTracking()
                    .SingleOrDefaultAsync(r =>
                        r.Id == order.CurrentApprovedResultRevisionId.Value &&
                        r.SurveyOrderId == order.Id &&
                        r.TenantId == order.TenantId &&
                        r.FarmId == order.FarmId &&
                        r.IsCurrent,
                        cancellationToken);
                if (currentApproval is null)
                {
                    return Result.Failure<SurveyResultReviewResponse>(
                        AppError.Conflict("ApprovedResult.Invalid", "Current internal approved result revision is missing or no longer current."));
                }
            }

            var activeAttempt = await FindAttemptForReviewReadAsync(order, currentApproval, cancellationToken);
            if (activeAttempt is null)
                return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("CaptureAttempt.NoActiveAttempt", "Survey order has no active or accepted capture attempt."));

            if (!order.VerifiedPoleCountSnapshotId.HasValue)
                return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("CountSnapshot.Missing", "Survey order does not have a verified pole count snapshot pinned."));

            var snapshot = await db.Set<SurveyOrderPoleCountSnapshot>()
                .AsNoTracking()
                .Include(s => s.Items)
                .SingleOrDefaultAsync(s => s.Id == order.VerifiedPoleCountSnapshotId.Value &&
                                           s.SurveyOrderId == order.Id &&
                                           s.TenantId == order.TenantId &&
                                           s.FarmId == order.FarmId,
                    cancellationToken);

            if (snapshot is null)
                return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("CountSnapshot.NotFound", "Verified pole count snapshot was not found for this order."));

            if (snapshot.CaptureAttemptId != (activeAttempt.MappingSourceAttemptId ?? activeAttempt.Id) ||
                (activeAttempt.MappingSourceAttemptId.HasValue &&
                 !await DiseaseCaptureMappingProof.HasAcceptedSourceAsync(db, order, snapshot, cancellationToken)))
                return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("CountSnapshot.AttemptMismatch", "Verified pole count snapshot belongs to another capture attempt."));

            var mappingBatch = await db.Set<MappingCandidateBatch>().AsNoTracking()
                .SingleOrDefaultAsync(b => b.Id == snapshot.MappingCandidateBatchId &&
                                           b.TenantId == order.TenantId &&
                                           b.FarmId == order.FarmId,
                    cancellationToken);
            if (mappingBatch is null)
                return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("MappingBatch.NotFound", "Mapping candidate batch referenced by count snapshot was not found."));

            var baselineMapVersionId = order.RequiresBaselineMapping
                ? snapshot.FarmBaseMapVersionId
                : order.BaselineMapVersionId ?? Guid.Empty;

            if (baselineMapVersionId == Guid.Empty)
                return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("BaselineMap.NotPinned", "Follow-up order is missing a pinned baseline map version."));

            SurveyComboPendingReviewBatch? pendingBatch = null;
            SurveyComboReviewSummaryDto? reviewSummary = null;
            SurveyExpectedFreezeTokensDto expectedTokens;

            if (commercial.ServiceCode == SurveyInternalApprovedResultRevision.ServiceCombo && commercial.ScreensDisease)
            {
                var batchResult = await LoadAndVerifyPendingBatchHeadAsync(order, activeAttempt, forUpdate: false, cancellationToken);
                if (batchResult.IsFailure)
                    return Result.Failure<SurveyResultReviewResponse>(batchResult.Error);

                var (batch, payload, persistedBlockers) = batchResult.Value;
                pendingBatch = MapPendingBatch(batch, payload, persistedBlockers);

                var reviewHeads = await db.Set<SurveyComboReviewRevision>().AsNoTracking()
                    .Where(r => r.TenantId == order.TenantId &&
                                r.FarmId == order.FarmId &&
                                r.SurveyOrderId == order.Id &&
                                r.ComboBatchId == batch.Id &&
                                r.IsCurrent)
                    .ToListAsync(cancellationToken);

                if (reviewHeads.Count > 1)
                    return Result.Failure<SurveyResultReviewResponse>(AppError.Conflict("ComboReview.AmbiguousHead", "Multiple current COMBO review revisions detected."));

                if (reviewHeads.Count == 1)
                {
                    var head = reviewHeads[0];
                    var decisionsResult = head.ReadDecisions();
                    if (decisionsResult.IsFailure)
                        return Result.Failure<SurveyResultReviewResponse>(decisionsResult.Error);

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

                expectedTokens = new SurveyExpectedFreezeTokensDto(
                    OrderId: order.Id,
                    ExpectedOrderVersion: order.Version,
                    ExpectedAttemptId: activeAttempt.Id,
                    ExpectedCountSnapshotId: snapshot.Id,
                    ExpectedCountFingerprint: snapshot.EvidenceFingerprint,
                    ExpectedComboBatchId: batch.Id,
                    ExpectedComboSourceRevision: batch.SourceRevision,
                    ExpectedComboBatchFingerprint: batch.CanonicalFingerprint,
                    ExpectedComboReviewRevisionId: reviewSummary?.ReviewRevisionId,
                    ExpectedComboReviewFingerprint: reviewSummary?.DecisionFingerprint);
            }
            else
            {
                expectedTokens = new SurveyExpectedFreezeTokensDto(
                    OrderId: order.Id,
                    ExpectedOrderVersion: order.Version,
                    ExpectedAttemptId: activeAttempt.Id,
                    ExpectedCountSnapshotId: snapshot.Id,
                    ExpectedCountFingerprint: snapshot.EvidenceFingerprint,
                    ExpectedComboBatchId: null,
                    ExpectedComboSourceRevision: null,
                    ExpectedComboBatchFingerprint: null,
                    ExpectedComboReviewRevisionId: null,
                    ExpectedComboReviewFingerprint: null);
            }

            await tx.CommitAsync(cancellationToken);

            return Result.Success(new SurveyResultReviewResponse(
                OrderId: order.Id,
                TenantId: order.TenantId,
                FarmId: order.FarmId,
                OrderVersion: order.Version,
                OrderStatus: order.Status.ToString(),
                ServiceCode: commercial.ServiceCode,
                ScreensDisease: commercial.ScreensDisease,
                MappingKind: mappingBatch.MappingKind.ToString(),
                CaptureAttemptId: activeAttempt.Id,
                CaptureAttemptVersion: activeAttempt.Version,
                CaptureAttemptStatus: activeAttempt.Status.ToString(),
                MissionId: pendingBatch?.MissionId ?? snapshot.MissionId,
                FarmBaseMapVersionId: snapshot.FarmBaseMapVersionId,
                BaselineMapVersionId: baselineMapVersionId,
                FarmBoundaryVersionId: snapshot.FarmBoundaryVersionId,
                MappingCandidateBatchId: snapshot.MappingCandidateBatchId,
                VerifiedPoleCountSnapshotId: snapshot.Id,
                VerifiedBillablePoleCount: snapshot.VerifiedBillablePoleCount,
                CountSnapshotFingerprint: snapshot.EvidenceFingerprint,
                CountSnapshotRevision: snapshot.RevisionNumber,
                CurrentApprovedResultRevisionId: order.CurrentApprovedResultRevisionId,
                ComboPendingBatch: pendingBatch,
                CurrentComboReview: reviewSummary,
                ExpectedFreezeTokens: expectedTokens,
                CurrentApprovedResultFingerprint: currentApproval?.EvidenceFingerprint));
        }
        catch (Fault f)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<SurveyResultReviewResponse>(f.Error);
        }
        catch (PostgresException pg) when (IsConcurrencyOrLockError(pg))
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<SurveyResultReviewResponse>(
                AppError.Conflict("SurveyResultReview.ConcurrencyConflict", "Review context changed concurrently; please reload and retry."));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<Result<RecordSurveyComboReviewResponse>> RecordComboReviewAsync(
        RecordSurveyComboReviewCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Validation("Command.Required", "Command payload is required."));
        if (actorId == Guid.Empty)
            return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Unauthorized("SurveyComboReview.Unauthorized", "Valid actor is required."));

        var reviewReason = command.ReviewReason?.Trim();
        if (string.IsNullOrWhiteSpace(reviewReason))
            return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Validation("ReviewReason.Required", "ReviewReason is required."));

        var normalizedExpectedBatchFingerprint = command.ExpectedBatchFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Hex64Regex.IsMatch(normalizedExpectedBatchFingerprint))
            return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Validation("ExpectedBatchFingerprint.Invalid", "ExpectedBatchFingerprint must be a 64-character lowercase hex string."));

        var normalizedDecisions = SurveyComboReviewRevision.CanonicalizeDecisions(
            new ComboReviewDecisionsSnapshotRecord(
                command.FindingDecisions.Select(f => new ComboFindingReviewDecisionRecord(
                    f.FindingId, f.Decision, f.Reason, f.EvidenceMediaIds ?? [])).ToList(),
                command.ZoneDecisions.Select(z => new ComboZoneReviewDecisionRecord(
                    z.SourceProposalId, z.Decision, z.ReviewedGeometry, z.ReviewedFindingIds,
                    z.SelectedGuidanceId, z.SelectedGuidanceVersionNumber, z.Reason, z.EvidenceMediaIds ?? [])).ToList(),
                command.BlockerResolutions.Select(b => new ComboBlockerResolutionRecord(
                    b.SourceType, b.SourceId, b.ReasonCode, b.Resolution, b.Reason, b.EvidenceMediaIds ?? [])).ToList()));

        var requestFingerprint = ComputeRequestFingerprint(new
        {
            command.OrderId,
            Action = SurveyResultReviewOperation.ActionRecordComboReview,
            command.ExpectedOrderVersion,
            command.ExpectedBatchId,
            command.ExpectedSourceRevision,
            ExpectedBatchFingerprint = normalizedExpectedBatchFingerprint,
            command.ExpectedPreviousReviewId,
            Outcome = SurveyComboReviewRevision.CanonicalizeOutcome(command.Outcome),
            ReviewReason = reviewReason,
            command.ConfirmNoDiseaseObserved,
            NegativeResultConfirmationEvidence = string.IsNullOrWhiteSpace(command.NegativeResultConfirmationEvidence)
                ? null
                : command.NegativeResultConfirmationEvidence.Trim(),
            Decisions = normalizedDecisions
        });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var scope = await db.Set<SurveyOrder>().AsNoTracking()
                .Where(o => o.Id == command.OrderId)
                .Select(o => new OrderScopeRow(o.Id, o.TenantId, o.FarmId, o.PreviousCompatibleOrderId))
                .SingleOrDefaultAsync(cancellationToken);

            if (scope is null)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

            // ADR 0020 Rank 1: Assignment authorization BEFORE receipt lookup or evidence reads
            if (!await IsAssignedManagerAsync(scope.TenantId, scope.FarmId, actorId, cancellationToken))
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Forbidden("SurveyResultReview.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            // ADR 0020 Rank 2: Lock Order(s) in ascending ID order
            var order = await LockRelatedOrdersAndGetCurrentAsync(scope, cancellationToken);
            if (order.TenantId != scope.TenantId || order.FarmId != scope.FarmId)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Forbidden("SurveyResultReview.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            // ADR 0020 Rank 3: Check Operation Receipt (replay works even if mutable state changed afterwards)
            var existingOp = await db.Set<SurveyResultReviewOperation>().AsNoTracking()
                .SingleOrDefaultAsync(op =>
                    op.TenantId == order.TenantId &&
                    op.ActorId == actorId &&
                    op.Action == SurveyResultReviewOperation.ActionRecordComboReview &&
                    op.ApiVersion == SurveyResultReviewOperation.ApiVersionV3 &&
                    op.OperationId == command.OperationId,
                    cancellationToken);

            if (existingOp is not null)
            {
                if (existingOp.SurveyOrderId != order.Id ||
                    !string.Equals(existingOp.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                {
                    return Result.Failure<RecordSurveyComboReviewResponse>(
                        AppError.Conflict("Operation.Conflict", "OperationId was already used with a different request payload or order."));
                }

                var replayed = existingOp.Outcome.Deserialize<RecordSurveyComboReviewResponse>(PayloadJsonOptions)
                    ?? throw new InvalidOperationException("Stored COMBO review receipt outcome cannot be deserialized.");
                await tx.CommitAsync(cancellationToken);
                return Result.Success(replayed with { IsReplay = true });
            }

            if (order.Scope != ContractScope.V3)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Conflict("SurveyOrder.ScopeInvalid", "Only V3 orders are supported."));

            if (order.Status != SurveyOrderStatus.Processing)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Conflict("SurveyOrder.StatusInvalid", $"Order must be in Processing status to record review (current: {order.Status})."));

            if (order.Version != command.ExpectedOrderVersion)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Conflict("SurveyOrder.VersionConflict", $"Expected order version {command.ExpectedOrderVersion}, actual {order.Version}."));

            var commercial = await db.Set<SurveyOrderCommercialSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(c => c.SurveyOrderId == order.Id, cancellationToken);
            if (commercial is null || commercial.ServiceCode != SurveyInternalApprovedResultRevision.ServiceCombo || !commercial.ScreensDisease)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Conflict("Service.Unsupported", "Order commercial snapshot must be COMBO with ScreensDisease enabled."));

            // ADR 0020 Rank 7: Lock active CaptureAttempt
            var attempt = await FindActiveAttemptAsync(order, forUpdate: true, cancellationToken);
            if (attempt is null || attempt.Status != CaptureAttemptStatus.Processing)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Conflict("CaptureAttempt.NotProcessing", "An active Processing capture attempt is required."));

            // Load and verify pending COMBO batch head under Order & Attempt lock
            var batchResult = await LoadAndVerifyPendingBatchHeadAsync(order, attempt, forUpdate: false, cancellationToken);
            if (batchResult.IsFailure)
                return Result.Failure<RecordSurveyComboReviewResponse>(batchResult.Error);

            var (batch, payload, persistedBlockers) = batchResult.Value;

            if (batch.Id != command.ExpectedBatchId || batch.SourceRevision != command.ExpectedSourceRevision)
            {
                return Result.Failure<RecordSurveyComboReviewResponse>(
                    AppError.Conflict("ComboBatch.Stale", "Expected COMBO batch ID or source revision does not match the current pending batch head."));
            }

            if (!string.Equals(batch.CanonicalFingerprint, normalizedExpectedBatchFingerprint, StringComparison.Ordinal))
            {
                return Result.Failure<RecordSurveyComboReviewResponse>(
                    AppError.Conflict("ComboBatch.FingerprintMismatch", "Expected COMBO batch fingerprint does not match current batch fingerprint."));
            }

            // Lock Mission & Job (Rank 8)
            var mission = await LockMissionForShareAsync(batch.MissionId, order.FarmId, cancellationToken);
            if (mission is null || mission.TenantId != order.TenantId || mission.SurveyOrderId != order.Id || mission.CaptureAttemptId != attempt.Id)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Conflict("Mission.ContextMismatch", "Mission context does not match batch and active attempt."));

            var job = await db.Set<AiProcessingJob>()
                .FromSqlInterpolated($"SELECT * FROM mission.ai_processing_jobs WHERE id = {batch.JobId} AND mission_id = {batch.MissionId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);
            if (job is null || job.Status != AiJobStatus.Completed || job.JobType != AiJobType.DiseaseDetection)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Conflict("ProcessingJob.Invalid", "Associated COMBO AI processing job is missing or not completed."));

            // Resolve review head on current batch
            var reviewsOnBatch = await db.Set<SurveyComboReviewRevision>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_combo_review_revisions WHERE combo_batch_id = {batch.Id} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR UPDATE")
                .ToListAsync(cancellationToken);

            var currentReviewHeads = reviewsOnBatch.Where(r => r.IsCurrent).ToList();
            if (currentReviewHeads.Count > 1)
                return Result.Failure<RecordSurveyComboReviewResponse>(AppError.Conflict("ComboReview.AmbiguousHead", "Multiple current review heads exist for this batch."));

            var currentReview = currentReviewHeads.SingleOrDefault();
            if (currentReview is null && command.ExpectedPreviousReviewId.HasValue)
            {
                return Result.Failure<RecordSurveyComboReviewResponse>(
                    AppError.Conflict("ComboReview.ExpectedPreviousMismatch", "ExpectedPreviousReviewId must be null when creating the initial review revision."));
            }
            if (currentReview is not null && command.ExpectedPreviousReviewId != currentReview.Id)
            {
                return Result.Failure<RecordSurveyComboReviewResponse>(
                    AppError.Conflict("ComboReview.ExpectedPreviousMismatch", $"ExpectedPreviousReviewId does not match current review head '{currentReview.Id}'."));
            }

            var nextReviewNumber = currentReview is null ? 1 : currentReview.RevisionNumber + 1;
            var now = ComputeNormalizedUtcTimestamp(order.UpdatedAt, attempt.UpdatedAt, currentReview?.ReviewedAt);

            // Validate allowed source media, findings, zones, guidance, bounds, blockers
            var allowedMedia = await LoadAllowedComboEvidenceAsync(batch, payload, cancellationToken);
            var reviewValidation = await ValidateComboReviewDecisionsAsync(
                order, batch, payload, persistedBlockers, normalizedDecisions,
                allowedMedia, job, command.ConfirmNoDiseaseObserved, command.NegativeResultConfirmationEvidence,
                SurveyComboReviewRevision.CanonicalizeOutcome(command.Outcome), now, cancellationToken);

            if (reviewValidation.IsFailure)
                return Result.Failure<RecordSurveyComboReviewResponse>(reviewValidation.Error);

            var newReviewId = Guid.CreateVersion7();
            var decisionFingerprint = SurveyComboReviewRevision.ComputeDecisionFingerprint(
                order.TenantId, order.FarmId, order.Id, attempt.Id, batch.Id, batch.SourceRevision,
                batch.CanonicalFingerprint, nextReviewNumber, currentReview?.Id,
                command.Outcome, reviewReason, command.ConfirmNoDiseaseObserved,
                command.NegativeResultConfirmationEvidence, actorId, now, normalizedDecisions);

            var newReviewResult = SurveyComboReviewRevision.Create(
                id: newReviewId,
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                captureAttemptId: attempt.Id,
                comboBatchId: batch.Id,
                sourceRevision: batch.SourceRevision,
                batchFingerprint: batch.CanonicalFingerprint,
                revisionNumber: nextReviewNumber,
                predecessorReviewId: currentReview?.Id,
                outcome: command.Outcome,
                reviewReason: reviewReason,
                confirmNoDiseaseObserved: command.ConfirmNoDiseaseObserved,
                negativeResultConfirmationEvidence: command.NegativeResultConfirmationEvidence,
                reviewedBy: actorId,
                reviewedAt: now,
                decisions: normalizedDecisions,
                decisionFingerprint: decisionFingerprint);

            if (newReviewResult.IsFailure)
                return Result.Failure<RecordSurveyComboReviewResponse>(newReviewResult.Error);

            var newReview = newReviewResult.Value;

            if (currentReview is not null)
            {
                var supersedeResult = currentReview.Supersede(actorId, now, "Superseded by newer COMBO review revision", newReview.Id);
                if (supersedeResult.IsFailure)
                    return Result.Failure<RecordSurveyComboReviewResponse>(supersedeResult.Error);

                // Flush supersede BEFORE inserting new IsCurrent = true row to obey unique filtered index
                await db.SaveChangesAsync(cancellationToken);
            }

            db.Set<SurveyComboReviewRevision>().Add(newReview);

            var unresolvedBlockers = normalizedDecisions.BlockerResolutions
                .Where(b => !SurveyComboReviewRevision.IsResolvedBlockerStatus(b.Resolution))
                .Select(b => $"{b.SourceType}:{b.SourceId}:{b.ReasonCode}")
                .ToList();

            var response = new RecordSurveyComboReviewResponse(
                OrderId: order.Id,
                BatchId: batch.Id,
                SourceRevision: batch.SourceRevision,
                ReviewRevisionId: newReview.Id,
                ReviewRevisionNumber: newReview.RevisionNumber,
                PredecessorReviewId: newReview.PredecessorReviewId,
                DecisionFingerprint: newReview.DecisionFingerprint,
                Outcome: newReview.Outcome,
                UnresolvedBlockersCount: newReview.UnresolvedBlockersCount,
                UnresolvedBlockers: unresolvedBlockers,
                ReviewedAt: newReview.ReviewedAt,
                IsReplay: false);

            var outcomeDoc = JsonSerializer.SerializeToDocument(response, CanonicalJsonOptions);
            var opResult = SurveyResultReviewOperation.Create(
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                actorId: actorId,
                action: SurveyResultReviewOperation.ActionRecordComboReview,
                apiVersion: SurveyResultReviewOperation.ApiVersionV3,
                operationId: command.OperationId,
                requestFingerprint: requestFingerprint,
                outcome: outcomeDoc,
                createdAt: now,
                comboBatchId: batch.Id,
                reviewRevisionId: newReview.Id,
                captureAttemptId: attempt.Id);

            if (opResult.IsFailure)
                return Result.Failure<RecordSurveyComboReviewResponse>(opResult.Error);

            db.Set<SurveyResultReviewOperation>().Add(opResult.Value);

            var correlationId = ResolveCorrelationId();
            using var auditDoc = JsonSerializer.SerializeToDocument(new
            {
                batch.Id,
                batch.SourceRevision,
                newReview.RevisionNumber,
                newReview.PredecessorReviewId,
                newReview.Outcome,
                newReview.DecisionFingerprint,
                newReview.AcceptedFindingsCount,
                newReview.ApprovedZonesCount,
                newReview.UnresolvedBlockersCount
            });
            db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                order.TenantId,
                order.FarmId,
                actorId,
                correlationId,
                nameof(SurveyComboReviewRevision),
                newReview.Id,
                $"COMBO_REVIEW_{newReview.Outcome.ToUpperInvariant()}",
                oldData: null,
                newData: auditDoc,
                createdAt: now));

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result.Success(response);
        }
        catch (Fault f)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<RecordSurveyComboReviewResponse>(f.Error);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException or PostgresException or SurveyDomainException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<RecordSurveyComboReviewResponse>(MapPersistenceException("SurveyComboReview", ex));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<Result<FreezeSurveyInternalResultResponse>> FreezeInternalResultAsync(
        FreezeSurveyInternalResultCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Validation("Command.Required", "Command payload is required."));
        if (actorId == Guid.Empty)
            return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Unauthorized("FreezeResult.Unauthorized", "Valid actor is required."));

        var freezeReason = command.FreezeReason?.Trim();
        if (string.IsNullOrWhiteSpace(freezeReason))
            return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Validation("FreezeReason.Required", "FreezeReason is required."));

        var normalizedExpectedCountFingerprint = command.ExpectedCountFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Hex64Regex.IsMatch(normalizedExpectedCountFingerprint))
            return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Validation("ExpectedCountFingerprint.Invalid", "ExpectedCountFingerprint must be a 64-character lowercase hex string."));

        var normalizedComboBatchFingerprint = string.IsNullOrWhiteSpace(command.ExpectedComboBatchFingerprint)
            ? null
            : command.ExpectedComboBatchFingerprint.Trim().ToLowerInvariant();
        var normalizedComboReviewFingerprint = string.IsNullOrWhiteSpace(command.ExpectedComboReviewFingerprint)
            ? null
            : command.ExpectedComboReviewFingerprint.Trim().ToLowerInvariant();

        var requestFingerprint = ComputeRequestFingerprint(new
        {
            command.OrderId,
            Action = SurveyResultReviewOperation.ActionFreezeInternalResult,
            command.ExpectedOrderVersion,
            command.ExpectedAttemptId,
            command.ExpectedCountSnapshotId,
            ExpectedCountFingerprint = normalizedExpectedCountFingerprint,
            ReviewDecision = SurveyInternalApprovedResultRevision.CanonicalizeReviewDecision(command.ReviewDecision),
            FreezeReason = freezeReason,
            command.ExpectedComboBatchId,
            command.ExpectedComboSourceRevision,
            ExpectedComboBatchFingerprint = normalizedComboBatchFingerprint,
            command.ExpectedComboReviewRevisionId,
            ExpectedComboReviewFingerprint = normalizedComboReviewFingerprint
        });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var scope = await db.Set<SurveyOrder>().AsNoTracking()
                .Where(o => o.Id == command.OrderId)
                .Select(o => new OrderScopeRow(o.Id, o.TenantId, o.FarmId, o.PreviousCompatibleOrderId))
                .SingleOrDefaultAsync(cancellationToken);

            if (scope is null)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

            // ADR 0020 Rank 1: Assignment authorization BEFORE receipt lookup or evidence reads
            if (!await IsAssignedManagerAsync(scope.TenantId, scope.FarmId, actorId, cancellationToken))
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Forbidden("SurveyResultReview.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            // ADR 0020 Rank 2: Lock Order(s) FOR UPDATE
            var order = await LockRelatedOrdersAndGetCurrentAsync(scope, cancellationToken);
            if (order.TenantId != scope.TenantId || order.FarmId != scope.FarmId)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Forbidden("SurveyResultReview.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            // ADR 0020 Rank 3: Replay / Operation Receipt check (works even after state moved to Accepted / AwaitingSettlement)
            var existingOp = await db.Set<SurveyResultReviewOperation>().AsNoTracking()
                .SingleOrDefaultAsync(op =>
                    op.TenantId == order.TenantId &&
                    op.ActorId == actorId &&
                    op.Action == SurveyResultReviewOperation.ActionFreezeInternalResult &&
                    op.ApiVersion == SurveyResultReviewOperation.ApiVersionV3 &&
                    op.OperationId == command.OperationId,
                    cancellationToken);

            if (existingOp is not null)
            {
                if (existingOp.SurveyOrderId != order.Id ||
                    !string.Equals(existingOp.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("Operation.Conflict", "OperationId was already used with a different request payload or order."));
                }

                var replayed = existingOp.Outcome.Deserialize<FreezeSurveyInternalResultResponse>(PayloadJsonOptions)
                    ?? throw new InvalidOperationException("Stored freeze receipt outcome cannot be deserialized.");
                await tx.CommitAsync(cancellationToken);
                return Result.Success(replayed with { IsReplay = true });
            }

            if (order.Scope != ContractScope.V3)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("SurveyOrder.ScopeInvalid", "Only V3 orders can execute internal result freeze."));

            if (order.Status != SurveyOrderStatus.Processing)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("SurveyOrder.StatusInvalid", $"Order must be in Processing status for first freeze (current: {order.Status})."));

            if (order.CurrentApprovedResultRevisionId.HasValue)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("FreezeResult.AlreadyFrozen", "An internal approved result revision already exists; corrections belong to invoice/correction flow."));

            if (order.Version != command.ExpectedOrderVersion)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("SurveyOrder.VersionConflict", $"Expected order version {command.ExpectedOrderVersion}, actual {order.Version}."));

            var commercial = await db.Set<SurveyOrderCommercialSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(c => c.SurveyOrderId == order.Id, cancellationToken);
            if (commercial is null)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CommercialSnapshot.Missing", "Pinned commercial snapshot was not found."));

            var isCombo = commercial.ServiceCode == SurveyInternalApprovedResultRevision.ServiceCombo && commercial.ScreensDisease;
            var isMap = commercial.ServiceCode == SurveyInternalApprovedResultRevision.ServiceMap && !commercial.ScreensDisease;
            if (!isCombo && !isMap)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CommercialSnapshot.Unsupported", $"Unsupported service capability '{commercial.ServiceCode}' (ScreensDisease={commercial.ScreensDisease})."));

            if (isMap && (command.ExpectedComboBatchId.HasValue || command.ExpectedComboSourceRevision.HasValue ||
                          normalizedComboBatchFingerprint is not null || command.ExpectedComboReviewRevisionId.HasValue ||
                          normalizedComboReviewFingerprint is not null))
            {
                return Result.Failure<FreezeSurveyInternalResultResponse>(
                    AppError.Validation("FreezeResult.ComboTokensInvalidForMap", "MAP orders must not specify COMBO batch or review tokens."));
            }

            if (isCombo && (!command.ExpectedComboBatchId.HasValue || !command.ExpectedComboSourceRevision.HasValue ||
                            normalizedComboBatchFingerprint is null || !command.ExpectedComboReviewRevisionId.HasValue ||
                            normalizedComboReviewFingerprint is null))
            {
                return Result.Failure<FreezeSurveyInternalResultResponse>(
                    AppError.Validation("FreezeResult.ComboTokensRequired", "COMBO orders require exact expected batch and review tokens."));
            }

            // ADR 0020 Rank 6: Lock and verify Baseline / Map / Boundary / Snapshot / MappingBatch proofs
            if (!order.VerifiedPoleCountSnapshotId.HasValue)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CountSnapshot.Missing", "Order does not have a verified pole count snapshot pinned."));

            if (order.VerifiedPoleCountSnapshotId.Value != command.ExpectedCountSnapshotId)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CountSnapshot.Stale", "Expected count snapshot ID does not match the order pinned snapshot."));

            var snapshot = await db.Set<SurveyOrderPoleCountSnapshot>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_order_pole_count_snapshots WHERE id = {order.VerifiedPoleCountSnapshotId.Value} FOR SHARE")
                .Include(s => s.Items)
                .SingleOrDefaultAsync(cancellationToken);

            if (snapshot is null || snapshot.SurveyOrderId != order.Id || snapshot.TenantId != order.TenantId || snapshot.FarmId != order.FarmId)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CountSnapshot.ContextMismatch", "Verified pole count snapshot does not match order context."));

            var mappingBatch = await db.Set<MappingCandidateBatch>()
                .FromSqlInterpolated($"SELECT *, xmin FROM farm.mapping_candidate_batches WHERE id = {snapshot.MappingCandidateBatchId} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);
            var baseMap = await db.Set<FarmBaseMapVersion>()
                .FromSqlInterpolated($"SELECT *, xmin FROM farm.farm_base_map_versions WHERE id = {snapshot.FarmBaseMapVersionId} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);
            var boundary = await db.Set<FarmBoundary>()
                .FromSqlInterpolated($"SELECT * FROM farm.farm_boundaries WHERE id = {snapshot.FarmBoundaryVersionId} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            if (mappingBatch is null || baseMap is null || boundary is null)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("BaselineProof.Missing", "Mapping batch, base map, or boundary proof could not be found."));

            if (mappingBatch.Status is not (MappingCandidateBatchStatus.Approved or MappingCandidateBatchStatus.Applied))
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("MappingBatch.NotApproved", "Mapping candidate batch is not in an approved or applied state."));

            if (boundary.ReviewedAt is null || boundary.Status is not (FarmBoundaryStatus.Approved or FarmBoundaryStatus.Superseded))
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("Boundary.NotApproved", "Capture farm boundary is not in a valid approved state."));

            if (baseMap.Status is not (FarmBaseMapStatus.InternalApproved or FarmBaseMapStatus.Published) ||
                baseMap.FarmBoundaryVersionId != boundary.Id)
            {
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("BaseMap.Invalid", "Capture map must be internally approved or published for the exact boundary."));
            }

            Guid baselineMapVersionId;
            if (order.RequiresBaselineMapping)
            {
                if (mappingBatch.MappingKind != MappingKind.FirstBaseline || !baseMap.IsBaseline || baseMap.BaselineMapVersionId != baseMap.Id)
                    return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("BaselineProof.InvalidFirstBaseline", "First-baseline order requires a FirstBaseline self-referencing baseline map."));
                baselineMapVersionId = baseMap.Id;
            }
            else
            {
                if (mappingBatch.MappingKind != MappingKind.FollowUpSnapshot || baseMap.IsBaseline ||
                    !order.BaselineMapVersionId.HasValue || baseMap.BaselineMapVersionId != order.BaselineMapVersionId.Value)
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("BaselineProof.InvalidFollowUp", "Follow-up order must keep its pinned baseline map version."));
                }
                baselineMapVersionId = order.BaselineMapVersionId.Value;
            }

            var recomputedSnapshotFingerprint = SurveyOrderPoleCountSnapshot.ComputeEvidenceFingerprint(
                snapshot.TenantId, snapshot.SurveyOrderId, snapshot.FarmId, snapshot.MissionId,
                snapshot.CaptureAttemptId, snapshot.FarmBaseMapVersionId, snapshot.FarmBoundaryVersionId,
                snapshot.MappingCandidateBatchId, mappingBatch.SpatialPolicyFingerprint, snapshot.Items);

            if (!string.Equals(recomputedSnapshotFingerprint, snapshot.EvidenceFingerprint, StringComparison.Ordinal) ||
                !string.Equals(snapshot.EvidenceFingerprint, normalizedExpectedCountFingerprint, StringComparison.Ordinal))
            {
                return Result.Failure<FreezeSurveyInternalResultResponse>(
                    AppError.Conflict("CountSnapshot.FingerprintMismatch", "Verified pole count snapshot fingerprint is stale or does not match immutable evidence."));
            }

            // ADR 0020 Rank 7: Lock active CaptureAttempt FOR UPDATE
            var attempt = await FindActiveAttemptAsync(order, forUpdate: true, cancellationToken);
            if (attempt is null)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CaptureAttempt.NoActiveAttempt", "Survey order has no active capture attempt."));

            if (attempt.Id != command.ExpectedAttemptId || attempt.Status != CaptureAttemptStatus.Processing)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CaptureAttempt.Stale", "Expected capture attempt is stale or no longer in Processing status."));

            var mappingAttemptId = attempt.MappingSourceAttemptId ?? attempt.Id;
            if (snapshot.CaptureAttemptId != mappingAttemptId || mappingBatch.CaptureAttemptId != mappingAttemptId ||
                (attempt.MappingSourceAttemptId.HasValue &&
                 (!isCombo || !await DiseaseCaptureMappingProof.HasAcceptedSourceAsync(db, order, snapshot, cancellationToken))))
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CaptureAttempt.SnapshotMismatch", "Count snapshot or mapping batch belongs to another capture attempt."));

            var missionId = await db.Set<DroneMission>().AsNoTracking()
                .Where(value => value.CaptureAttemptId == attempt.Id && value.SurveyOrderId == order.Id)
                .Select(value => (Guid?)value.Id).SingleOrDefaultAsync(cancellationToken);
            if (!missionId.HasValue || mappingBatch.MissionId != snapshot.MissionId ||
                (attempt.MappingSourceAttemptId is null && missionId.Value != snapshot.MissionId))
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("Mission.AttemptMismatch", "Attempt, count snapshot, and mapping batch disagree on execution Mission."));

            // ADR 0020 Rank 8: Lock DroneMission FOR SHARE
            var mission = await LockMissionForShareAsync(missionId.Value, order.FarmId, cancellationToken);
            if (mission is null || mission.TenantId != order.TenantId || mission.SurveyOrderId != order.Id || mission.CaptureAttemptId != attempt.Id ||
                (attempt.MappingSourceAttemptId.HasValue && !DiseaseCaptureMappingProof.MatchesDiseaseMission(snapshot, mission)))
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("Mission.ContextMismatch", "Execution Mission is missing or no longer bound to this order/attempt."));
            attempt.BindMission(mission.Id);

            // ADR 0020 Rank 10: Verify CaptureInputApproval & CaptureInputManifest
            if (string.IsNullOrWhiteSpace(attempt.InputApprovalReference) ||
                !Guid.TryParse(attempt.InputApprovalReference, out var inputApprovalId) ||
                string.IsNullOrWhiteSpace(attempt.ProcessingJobReference) ||
                !Guid.TryParse(attempt.ProcessingJobReference, out var attemptProcessingJobId))
            {
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CaptureAttempt.ApprovalMissing", "Capture attempt is missing approved input or processing job reference."));
            }

            var inputApproval = await db.Set<CaptureInputApproval>()
                .FromSqlInterpolated($"SELECT * FROM survey.capture_input_approvals WHERE id = {inputApprovalId} AND capture_attempt_id = {attempt.Id} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} AND mission_id = {missionId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            if (inputApproval is null || inputApproval.ProcessingJobId != attemptProcessingJobId)
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CaptureAttempt.ApprovalMismatch", "Capture input approval record does not match the active processing job reference."));

            var inputManifest = await db.Set<CaptureInputManifest>().AsNoTracking()
                .SingleOrDefaultAsync(m => m.Id == inputApproval.InputManifestId &&
                                           m.CaptureAttemptId == attempt.Id &&
                                           m.Status == CaptureInputManifestStatus.Approved,
                    cancellationToken);
            if (inputManifest is null || !string.Equals(inputManifest.ManifestFingerprint, inputApproval.ManifestFingerprint, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("CaptureManifest.Invalid", "Approved capture input manifest is missing or invalid."));

            // MAP vs COMBO branch evaluation
            Guid? comboBatchId = null;
            string? comboBatchFingerprint = null;
            Guid? comboReviewRevisionId = null;
            string? comboReviewFingerprint = null;
            string healthAssessmentStatus;
            string? limitationNotice = null;
            SurveyComboReviewRevision? approvedComboReview = null;

            if (isMap)
            {
                // Require the completed mapping job identified by the approved snapshot.
                var mappingJob = await db.Set<AiProcessingJob>()
                    .FromSqlInterpolated($"SELECT * FROM mission.ai_processing_jobs WHERE id = {mappingBatch.JobId} FOR SHARE")
                    .SingleOrDefaultAsync(cancellationToken);
                if (mappingJob is null || mappingJob.MissionId != missionId ||
                    mappingJob.JobType != AiJobType.Mapping || mappingJob.Status != AiJobStatus.Completed)
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("MappingJob.Invalid", "Referenced mapping processing job is invalid or incomplete for this mission."));
                }

                healthAssessmentStatus = SurveyInternalApprovedResultRevision.HealthNotAssessed;
            }
            else // COMBO
            {
                var batchResult = await LoadAndVerifyPendingBatchHeadAsync(order, attempt, forUpdate: false, cancellationToken);
                if (batchResult.IsFailure)
                    return Result.Failure<FreezeSurveyInternalResultResponse>(batchResult.Error);

                var (batch, payload, persistedBlockers) = batchResult.Value;

                if (batch.Id != command.ExpectedComboBatchId!.Value ||
                    batch.SourceRevision != command.ExpectedComboSourceRevision!.Value ||
                    !string.Equals(batch.CanonicalFingerprint, normalizedComboBatchFingerprint, StringComparison.Ordinal))
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("ComboBatch.Stale", "Expected COMBO batch ID, source revision, or fingerprint no longer matches pending batch head."));
                }

                if (batch.MissionId != missionId ||
                    batch.MappingCandidateBatchId != snapshot.MappingCandidateBatchId ||
                    batch.FarmBaseMapVersionId != snapshot.FarmBaseMapVersionId ||
                    batch.BaselineMapVersionId != baselineMapVersionId ||
                    batch.FarmBoundaryVersionId != snapshot.FarmBoundaryVersionId ||
                    batch.InputApprovalId != inputApproval.Id ||
                    batch.InputManifestId != inputApproval.InputManifestId)
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("ComboBatch.ContextMismatch", "Pending COMBO batch source references do not match the frozen snapshot and capture approval."));
                }

                // Verify COMBO job execution, ApprovedJobReference binding, and full input coverage
                var comboJob = await db.Set<AiProcessingJob>()
                    .FromSqlInterpolated($"SELECT * FROM mission.ai_processing_jobs WHERE id = {batch.JobId} AND mission_id = {batch.MissionId} FOR SHARE")
                    .SingleOrDefaultAsync(cancellationToken);

                if (comboJob is null || comboJob.Status != AiJobStatus.Completed || comboJob.JobType != AiJobType.DiseaseDetection)
                    return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("ComboJob.NotCompleted", "Source COMBO AI processing job was not found in Completed state."));

                if (!comboJob.HasCompleteComboInputCoverage())
                    return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("ComboJob.IncompleteCoverage", "Source COMBO AI processing job lacks complete per-image coverage proof."));

                ComboAnalysisProcessingSnapshot? comboSnapshot;
                try
                {
                    comboSnapshot = comboJob.Parameters.Deserialize<ComboAnalysisProcessingSnapshot>();
                }
                catch (JsonException)
                {
                    comboSnapshot = null;
                }

                if (comboSnapshot is null ||
                    comboSnapshot.SourceRevision != batch.SourceRevision ||
                    comboSnapshot.ApprovedJobReference != inputApproval.ProcessingJobId)
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("ComboJob.ApprovalReferenceMismatch", "Source COMBO job ApprovedJobReference does not match approved attempt processing job."));
                }

                // Verify input manifest files equal COMBO job MediaIds
                var frozenFiles = await db.Set<CaptureInputManifestFile>().AsNoTracking()
                    .Where(f => f.ManifestId == inputApproval.InputManifestId && f.MediaRole == "RawImage")
                    .Select(f => f.MediaAssetId)
                    .ToListAsync(cancellationToken);

                Guid[]? jobMediaIds = null;
                if (comboJob.InputManifest.RootElement.ValueKind == JsonValueKind.Object &&
                    comboJob.InputManifest.RootElement.TryGetProperty("MediaIds", out var mediaProp))
                {
                    jobMediaIds = mediaProp.Deserialize<Guid[]>();
                }

                if (frozenFiles.Count == 0 || jobMediaIds is null || !frozenFiles.ToHashSet().SetEquals(jobMediaIds))
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("ComboJob.ManifestFilesMismatch", "Source COMBO job input media does not match approved frozen manifest files."));
                }

                // Reload and lock current review head on batch
                var reviewHeads = await db.Set<SurveyComboReviewRevision>()
                    .FromSqlInterpolated($"SELECT * FROM survey.survey_combo_review_revisions WHERE combo_batch_id = {batch.Id} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} AND is_current = true FOR SHARE")
                    .ToListAsync(cancellationToken);

                if (reviewHeads.Count != 1)
                    return Result.Failure<FreezeSurveyInternalResultResponse>(AppError.Conflict("ComboReview.Missing", "An active current COMBO review revision is required before freeze."));

                approvedComboReview = reviewHeads[0];
                if (approvedComboReview.Id != command.ExpectedComboReviewRevisionId!.Value ||
                    !string.Equals(approvedComboReview.DecisionFingerprint, normalizedComboReviewFingerprint, StringComparison.Ordinal))
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("ComboReview.Stale", "Expected COMBO review revision ID or fingerprint does not match the current review head."));
                }

                if (approvedComboReview.Outcome != SurveyComboReviewRevision.OutcomeApproved ||
                    approvedComboReview.UnresolvedBlockersCount > 0)
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("ComboReview.NotApproved", "COMBO review must be Approved with zero unresolved blockers before internal result freeze."));
                }

                var decisionsResult = approvedComboReview.ReadDecisions();
                if (decisionsResult.IsFailure)
                    return Result.Failure<FreezeSurveyInternalResultResponse>(decisionsResult.Error);

                var freezeEvaluationTime = ComputeNormalizedUtcTimestamp(order.UpdatedAt, attempt.UpdatedAt, approvedComboReview.ReviewedAt);
                var allowedMedia = await LoadAllowedComboEvidenceAsync(batch, payload, cancellationToken);
                var recheckValidation = await ValidateComboReviewDecisionsAsync(
                    order, batch, payload, persistedBlockers, decisionsResult.Value,
                    allowedMedia, comboJob, approvedComboReview.ConfirmNoDiseaseObserved,
                    approvedComboReview.NegativeResultConfirmationEvidence,
                    approvedComboReview.Outcome, freezeEvaluationTime, cancellationToken);

                if (recheckValidation.IsFailure)
                    return Result.Failure<FreezeSurveyInternalResultResponse>(recheckValidation.Error);

                var recomputedDecisionFp = SurveyComboReviewRevision.ComputeDecisionFingerprint(
                    approvedComboReview.TenantId, approvedComboReview.FarmId, approvedComboReview.SurveyOrderId,
                    approvedComboReview.CaptureAttemptId, approvedComboReview.ComboBatchId, approvedComboReview.SourceRevision,
                    approvedComboReview.BatchFingerprint, approvedComboReview.RevisionNumber, approvedComboReview.PredecessorReviewId,
                    approvedComboReview.Outcome, approvedComboReview.ReviewReason, approvedComboReview.ConfirmNoDiseaseObserved,
                    approvedComboReview.NegativeResultConfirmationEvidence, approvedComboReview.ReviewedBy, approvedComboReview.ReviewedAt,
                    decisionsResult.Value);

                if (!string.Equals(recomputedDecisionFp, approvedComboReview.DecisionFingerprint, StringComparison.Ordinal))
                {
                    return Result.Failure<FreezeSurveyInternalResultResponse>(
                        AppError.Conflict("ComboReview.FingerprintMismatch", "Stored COMBO review decision fingerprint failed integrity verification."));
                }

                comboBatchId = batch.Id;
                comboBatchFingerprint = batch.CanonicalFingerprint;
                comboReviewRevisionId = approvedComboReview.Id;
                comboReviewFingerprint = approvedComboReview.DecisionFingerprint;
                limitationNotice = payload.LimitationNotice;

                if (approvedComboReview.AcceptedFindingsCount > 0)
                {
                    healthAssessmentStatus = SurveyInternalApprovedResultRevision.HealthSuspectedObservationsApproved;
                }
                else
                {
                    // Negative result requires confirmed negative review + full coverage proof
                    if (!approvedComboReview.ConfirmNoDiseaseObserved)
                    {
                        return Result.Failure<FreezeSurveyInternalResultResponse>(
                            AppError.Conflict("FreezeResult.NegativeConfirmationRequired", "Zero accepted disease findings requires explicit negative review confirmation with complete coverage proof."));
                    }

                    healthAssessmentStatus = SurveyInternalApprovedResultRevision.HealthNoObservationsDetected;
                }
            }

            var frozenAt = ComputeNormalizedUtcTimestamp(order.UpdatedAt, attempt.UpdatedAt, approvedComboReview?.ReviewedAt);
            var approvedRevisionId = Guid.CreateVersion7();
            var evidenceFingerprint = SurveyInternalApprovedResultRevision.ComputeEvidenceFingerprint(
                tenantId: order.TenantId,
                surveyOrderId: order.Id,
                farmId: order.FarmId,
                revisionNumber: 1,
                serviceCode: commercial.ServiceCode,
                screensDisease: commercial.ScreensDisease,
                captureAttemptId: snapshot.CaptureAttemptId,
                missionId: snapshot.MissionId,
                farmBaseMapVersionId: snapshot.FarmBaseMapVersionId,
                baselineMapVersionId: baselineMapVersionId,
                farmBoundaryVersionId: snapshot.FarmBoundaryVersionId,
                poleCountSnapshotId: snapshot.Id,
                mappingCandidateBatchId: snapshot.MappingCandidateBatchId,
                comboBatchId: comboBatchId,
                reviewDecision: command.ReviewDecision,
                healthAssessmentStatus: healthAssessmentStatus,
                limitationNotice: limitationNotice,
                reviewedBy: actorId,
                reviewedAt: frozenAt,
                reviewReason: freezeReason,
                predecessorRevisionId: null,
                comboReviewRevisionId: comboReviewRevisionId,
                comboReviewFingerprint: comboReviewFingerprint);

            var approvedRevisionResult = SurveyInternalApprovedResultRevision.Create(
                id: approvedRevisionId,
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                revisionNumber: 1,
                serviceCode: commercial.ServiceCode,
                screensDisease: commercial.ScreensDisease,
                captureAttemptId: snapshot.CaptureAttemptId,
                missionId: snapshot.MissionId,
                farmBaseMapVersionId: snapshot.FarmBaseMapVersionId,
                baselineMapVersionId: baselineMapVersionId,
                farmBoundaryVersionId: snapshot.FarmBoundaryVersionId,
                poleCountSnapshotId: snapshot.Id,
                mappingCandidateBatchId: snapshot.MappingCandidateBatchId,
                comboBatchId: comboBatchId,
                reviewDecision: command.ReviewDecision,
                healthAssessmentStatus: healthAssessmentStatus,
                limitationNotice: limitationNotice,
                reviewedBy: actorId,
                reviewedAt: frozenAt,
                reviewReason: freezeReason,
                evidenceFingerprint: evidenceFingerprint,
                predecessorRevisionId: null,
                comboReviewRevisionId: comboReviewRevisionId,
                comboReviewFingerprint: comboReviewFingerprint);

            if (approvedRevisionResult.IsFailure)
                return Result.Failure<FreezeSurveyInternalResultResponse>(approvedRevisionResult.Error);

            var approvedRevision = approvedRevisionResult.Value;
            db.Set<SurveyInternalApprovedResultRevision>().Add(approvedRevision);

            // Persist revision first (deferred pointer FK integrity) then update Order pointer & Attempt acceptance atomically
            var recordResult = order.RecordInternalApprovedResult(
                approvedRevision,
                currentRevision: null,
                countSnapshot: snapshot,
                expectedVersion: command.ExpectedOrderVersion);

            if (recordResult.IsFailure)
                return Result.Failure<FreezeSurveyInternalResultResponse>(recordResult.Error);

            var correlationId = ResolveCorrelationId();
            var acceptanceEvidence = new CaptureProcessingAcceptanceEvidence(
                tenantId: order.TenantId,
                farmId: order.FarmId,
                orderId: order.Id,
                attemptId: attempt.Id,
                missionId: mission.Id,
                processingJobId: attempt.ProcessingJobReference!,
                acceptanceReceiptId: approvedRevision.Id.ToString("D"));

            attempt.AcceptProcessing(
                acceptanceEvidence: acceptanceEvidence,
                timestamp: frozenAt,
                source: "V3_INTERNAL_RESULT_FREEZE",
                reason: freezeReason,
                actorId: actorId,
                correlationId: correlationId,
                operationId: command.OperationId,
                payloadFingerprint: requestFingerprint,
                expectedVersion: attempt.Version);

            var response = new FreezeSurveyInternalResultResponse(
                OrderId: order.Id,
                OrderVersion: order.Version,
                OrderStatus: order.Status.ToString(),
                ServiceCode: approvedRevision.ServiceCode,
                ScreensDisease: approvedRevision.ScreensDisease,
                ApprovedResultRevisionId: approvedRevision.Id,
                ApprovedResultRevisionNumber: approvedRevision.RevisionNumber,
                ApprovedResultFingerprint: approvedRevision.EvidenceFingerprint,
                ReviewDecision: approvedRevision.ReviewDecision,
                HealthAssessmentStatus: approvedRevision.HealthAssessmentStatus,
                LimitationNotice: approvedRevision.LimitationNotice,
                AttemptId: attempt.Id,
                AttemptVersion: attempt.Version,
                AttemptStatus: attempt.Status.ToString(),
                MissionId: mission.Id,
                CountSnapshotId: snapshot.Id,
                VerifiedBillablePoleCount: snapshot.VerifiedBillablePoleCount,
                CountSnapshotFingerprint: snapshot.EvidenceFingerprint,
                FarmBaseMapVersionId: approvedRevision.FarmBaseMapVersionId,
                BaselineMapVersionId: approvedRevision.BaselineMapVersionId,
                FarmBoundaryVersionId: approvedRevision.FarmBoundaryVersionId,
                MappingCandidateBatchId: approvedRevision.MappingCandidateBatchId,
                ComboBatchId: approvedRevision.ComboBatchId,
                ComboBatchFingerprint: comboBatchFingerprint,
                ComboReviewRevisionId: approvedRevision.ComboReviewRevisionId,
                ComboReviewFingerprint: approvedRevision.ComboReviewFingerprint,
                FrozenAt: frozenAt,
                IsReplay: false);

            var outcomeDoc = JsonSerializer.SerializeToDocument(response, CanonicalJsonOptions);
            var opResult = SurveyResultReviewOperation.Create(
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                actorId: actorId,
                action: SurveyResultReviewOperation.ActionFreezeInternalResult,
                apiVersion: SurveyResultReviewOperation.ApiVersionV3,
                operationId: command.OperationId,
                requestFingerprint: requestFingerprint,
                outcome: outcomeDoc,
                createdAt: frozenAt,
                comboBatchId: approvedRevision.ComboBatchId,
                reviewRevisionId: approvedRevision.ComboReviewRevisionId,
                approvedResultRevisionId: approvedRevision.Id,
                captureAttemptId: attempt.Id,
                poleCountSnapshotId: snapshot.Id);

            if (opResult.IsFailure)
                return Result.Failure<FreezeSurveyInternalResultResponse>(opResult.Error);

            db.Set<SurveyResultReviewOperation>().Add(opResult.Value);

            using var auditDoc = JsonSerializer.SerializeToDocument(new
            {
                approvedRevision.Id,
                approvedRevision.RevisionNumber,
                approvedRevision.ServiceCode,
                approvedRevision.HealthAssessmentStatus,
                approvedRevision.PoleCountSnapshotId,
                approvedRevision.ComboBatchId,
                approvedRevision.ComboReviewRevisionId,
                approvedRevision.EvidenceFingerprint,
                AttemptId = attempt.Id,
                AttemptStatus = attempt.Status.ToString()
            });
            db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                order.TenantId,
                order.FarmId,
                actorId,
                correlationId,
                nameof(SurveyInternalApprovedResultRevision),
                approvedRevision.Id,
                "INTERNAL_RESULT_FROZEN",
                oldData: null,
                newData: auditDoc,
                createdAt: frozenAt));

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result.Success(response);
        }
        catch (Fault f)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<FreezeSurveyInternalResultResponse>(f.Error);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException or PostgresException or SurveyDomainException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<FreezeSurveyInternalResultResponse>(MapPersistenceException("FreezeResult", ex));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<SurveyOrder> LockRelatedOrdersAndGetCurrentAsync(OrderScopeRow scope, CancellationToken cancellationToken)
    {
        var idsToLock = scope.PreviousCompatibleOrderId.HasValue && scope.PreviousCompatibleOrderId.Value != scope.Id
            ? new[] { scope.Id, scope.PreviousCompatibleOrderId.Value }.Order().ToArray()
            : [scope.Id];

        SurveyOrder? current = null;
        foreach (var id in idsToLock)
        {
            var locked = await db.Set<SurveyOrder>()
                .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (id == scope.Id)
                current = locked;
        }

        return current ?? throw new Fault(AppError.NotFound("Order.NotFound", "Survey order was not found."));
    }

    private async Task<CaptureAttempt?> FindAttemptForReviewReadAsync(
        SurveyOrder order,
        SurveyInternalApprovedResultRevision? currentApproval,
        CancellationToken cancellationToken)
    {
        if (currentApproval is not null)
        {
            var attemptId = currentApproval.CaptureAttemptId;
            if (currentApproval.ComboBatchId is Guid comboBatchId)
            {
                var comboAttemptId = await db.Set<SurveyComboImportBatch>().AsNoTracking()
                    .Where(b => b.Id == comboBatchId && b.SurveyOrderId == order.Id &&
                        b.TenantId == order.TenantId && b.FarmId == order.FarmId)
                    .Select(b => (Guid?)b.CaptureAttemptId).SingleOrDefaultAsync(cancellationToken);
                if (!comboAttemptId.HasValue)
                    return null;

                attemptId = comboAttemptId.Value;
            }

            return await db.Set<CaptureAttempt>().AsNoTracking()
                .Where(a => a.Id == attemptId &&
                            a.OrderId == order.Id &&
                            a.TenantId == order.TenantId &&
                            a.FarmId == order.FarmId &&
                            (a.Status == CaptureAttemptStatus.Accepted ||
                             a.Status == CaptureAttemptStatus.Processing))
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await FindActiveAttemptAsync(order, forUpdate: false, cancellationToken);
    }

    private async Task<CaptureAttempt?> FindActiveAttemptAsync(SurveyOrder order, bool forUpdate, CancellationToken cancellationToken)
    {
        if (forUpdate)
        {
            return await db.Set<CaptureAttempt>()
                .FromSqlInterpolated($"""
                    SELECT *, xmin FROM survey.capture_attempts
                    WHERE order_id = {order.Id}
                      AND tenant_id = {order.TenantId}
                      AND farm_id = {order.FarmId}
                      AND status IN ('PLANNED', 'PREFLIGHT_READY', 'IN_FLIGHT', 'AWAITING_UPLOAD', 'UPLOADED', 'PROCESSING')
                    FOR UPDATE
                    """)
                .Include(a => a.History)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await db.Set<CaptureAttempt>().AsNoTracking()
            .Where(a => a.OrderId == order.Id &&
                        a.TenantId == order.TenantId &&
                        a.FarmId == order.FarmId &&
                        (a.Status == CaptureAttemptStatus.Planned ||
                         a.Status == CaptureAttemptStatus.PreflightReady ||
                         a.Status == CaptureAttemptStatus.InFlight ||
                         a.Status == CaptureAttemptStatus.AwaitingUpload ||
                         a.Status == CaptureAttemptStatus.Uploaded ||
                         a.Status == CaptureAttemptStatus.Processing))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task<DroneMission?> LockMissionForShareAsync(Guid missionId, Guid farmId, CancellationToken cancellationToken) =>
        db.Set<DroneMission>()
            .FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {missionId} AND farm_id = {farmId} FOR SHARE")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<Result<(SurveyComboImportBatch Batch, ComboAnalysisReadyV1 Payload, List<ComboPendingBlockingItem> Blockers)>> LoadAndVerifyPendingBatchHeadAsync(
        SurveyOrder order,
        CaptureAttempt attempt,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var sql = forUpdate
            ? db.Set<SurveyComboImportBatch>().FromSqlInterpolated($"""
                SELECT * FROM survey.survey_combo_import_batches
                WHERE tenant_id = {order.TenantId}
                  AND farm_id = {order.FarmId}
                  AND survey_order_id = {order.Id}
                  AND capture_attempt_id = {attempt.Id}
                  AND status = {SurveyComboImportBatch.StatusPendingReview}
                FOR UPDATE
                """)
            : db.Set<SurveyComboImportBatch>().AsNoTracking()
                .Where(b => b.TenantId == order.TenantId &&
                            b.FarmId == order.FarmId &&
                            b.SurveyOrderId == order.Id &&
                            b.CaptureAttemptId == attempt.Id &&
                            b.Status == SurveyComboImportBatch.StatusPendingReview);

        var pendingBatches = await sql.ToListAsync(cancellationToken);
        if (pendingBatches.Count == 0)
        {
            return Result.Failure<(SurveyComboImportBatch, ComboAnalysisReadyV1, List<ComboPendingBlockingItem>)>(
                AppError.Conflict("ComboBatch.NotFound", "No pending COMBO batch exists for the active processing attempt."));
        }

        var predecessorIds = pendingBatches
            .Where(b => b.PredecessorBatchId.HasValue)
            .Select(b => b.PredecessorBatchId!.Value)
            .ToHashSet();

        var headCandidates = pendingBatches.Where(b => !predecessorIds.Contains(b.Id)).ToList();
        if (headCandidates.Count != 1)
        {
            return Result.Failure<(SurveyComboImportBatch, ComboAnalysisReadyV1, List<ComboPendingBlockingItem>)>(
                AppError.Conflict("ComboBatch.AmbiguousHead", "Multiple or ambiguous pending COMBO batch heads detected."));
        }

        var batch = headCandidates[0];

        ComboAnalysisReadyV1? payload;
        List<ComboPendingBlockingItem>? blockers;
        try
        {
            payload = batch.RawPayload.Deserialize<ComboAnalysisReadyV1>(PayloadJsonOptions);
            blockers = batch.BlockingItems.Deserialize<List<ComboPendingBlockingItem>>(PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return Result.Failure<(SurveyComboImportBatch, ComboAnalysisReadyV1, List<ComboPendingBlockingItem>)>(
                AppError.Validation("ComboBatch.CorruptedPayload", "Persisted COMBO batch JSON payload or blockers cannot be parsed."));
        }

        if (payload is null || blockers is null || blockers.Count != batch.BlockingCount)
        {
            return Result.Failure<(SurveyComboImportBatch, ComboAnalysisReadyV1, List<ComboPendingBlockingItem>)>(
                AppError.Conflict("ComboBatch.CorruptedData", "Persisted COMBO batch data or blocking item count is inconsistent."));
        }

        var recomputedPayloadFp = ComboAnalysisV1Fingerprint.ForPayload(payload);
        if (!string.Equals(recomputedPayloadFp, batch.CanonicalFingerprint, StringComparison.Ordinal))
        {
            return Result.Failure<(SurveyComboImportBatch, ComboAnalysisReadyV1, List<ComboPendingBlockingItem>)>(
                AppError.Conflict("ComboBatch.FingerprintMismatch", "Stored COMBO batch payload no longer matches its canonical fingerprint."));
        }

        return Result.Success((batch, payload, blockers));
    }

    private async Task<HashSet<Guid>> LoadAllowedComboEvidenceAsync(
        SurveyComboImportBatch batch,
        ComboAnalysisReadyV1 payload,
        CancellationToken cancellationToken)
    {
        var sourceIds = payload.Findings.SelectMany(finding => finding.EvidenceMedia)
            .Concat(payload.BoundaryExceptions.SelectMany(exception => exception.EvidenceMedia))
            .Concat(payload.DiseaseZones.SelectMany(zone => zone.EvidenceMedia))
            .Select(evidence => evidence.MediaAssetId)
            .Distinct()
            .ToArray();
        if (sourceIds.Length == 0)
            return [];

        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT mm.media_id AS "Value"
            FROM mission.mission_media mm
            JOIN media.media_assets ma ON ma.id = mm.media_id
            WHERE mm.mission_id = {batch.MissionId}
              AND ma.tenant_id = {batch.TenantId}
              AND ma.farm_id = {batch.FarmId}
              AND ma.deleted_at IS NULL
              AND mm.media_id = ANY({sourceIds})
            """).ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    internal async Task<Result> ValidateCorrectionComboReviewAsync(
        SurveyOrder order,
        SurveyComboImportBatch batch,
        ComboAnalysisReadyV1 payload,
        ComboReviewDecisionsSnapshotRecord decisions,
        bool confirmNoDiseaseObserved,
        string? negativeConfirmationEvidence,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        List<ComboPendingBlockingItem>? blockers;
        try
        {
            blockers = batch.BlockingItems.Deserialize<List<ComboPendingBlockingItem>>(PayloadJsonOptions);
        }
        catch (JsonException)
        {
            blockers = null;
        }

        if (blockers is null || blockers.Count != batch.BlockingCount ||
            !string.Equals(ComboAnalysisV1Fingerprint.ForPayload(payload), batch.CanonicalFingerprint, StringComparison.Ordinal))
        {
            return Result.Failure(AppError.Conflict("ComboBatch.CorruptedData", "COMBO batch payload or blocking evidence failed verification."));
        }

        var job = await db.Set<AiProcessingJob>()
            .AsNoTracking()
            .SingleOrDefaultAsync(j => j.Id == batch.JobId && j.MissionId == batch.MissionId, cancellationToken);
        if (job is null || job.Status != AiJobStatus.Completed || job.JobType != AiJobType.DiseaseDetection ||
            !job.HasCompleteComboInputCoverage())
        {
            return Result.Failure(AppError.Conflict("ComboJob.IncompleteCoverage", "The source COMBO job must be complete with verified input coverage."));
        }

        var allowedMedia = await LoadAllowedComboEvidenceAsync(batch, payload, cancellationToken);
        return await ValidateComboReviewDecisionsAsync(
            order, batch, payload, blockers, decisions, allowedMedia, job, confirmNoDiseaseObserved,
            negativeConfirmationEvidence, SurveyComboReviewRevision.OutcomeApproved, evaluatedAt, cancellationToken);
    }

    private async Task<Result> ValidateComboReviewDecisionsAsync(
        SurveyOrder order,
        SurveyComboImportBatch batch,
        ComboAnalysisReadyV1 payload,
        List<ComboPendingBlockingItem> persistedBlockers,
        ComboReviewDecisionsSnapshotRecord decisions,
        HashSet<Guid> allowedMedia,
        AiProcessingJob job,
        bool confirmNoDiseaseObserved,
        string? negativeConfirmationEvidence,
        string outcome,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        // 1. Findings: exact 1-to-1 coverage with batch findings
        var findingsById = payload.Findings.ToDictionary(f => f.FindingId);
        var findingDecisionById = new Dictionary<Guid, ComboFindingReviewDecisionRecord>();

        foreach (var fd in decisions.FindingDecisions)
        {
            if (!findingsById.ContainsKey(fd.FindingId))
            {
                return Result.Failure(AppError.Validation("ComboReview.UnknownFinding",
                    $"Finding decision references '{fd.FindingId}', which does not belong to COMBO batch '{batch.Id}'."));
            }

            if (!findingDecisionById.TryAdd(fd.FindingId, fd))
            {
                return Result.Failure(AppError.Validation("ComboReview.DuplicateFindingDecision",
                    $"Duplicate review decision for finding '{fd.FindingId}'."));
            }

            if (fd.Decision is not (SurveyComboReviewRevision.FindingDecisionAccept or SurveyComboReviewRevision.FindingDecisionExclude))
                return Result.Failure(AppError.Validation("ComboReview.InvalidFindingDecision", $"Invalid finding decision '{fd.Decision}'."));

            var mediaCheck = ValidateEvidenceMedia(fd.EvidenceMediaIds, allowedMedia,
                required: fd.Decision == SurveyComboReviewRevision.FindingDecisionExclude);
            if (mediaCheck.IsFailure) return mediaCheck;

            if (fd.Decision == SurveyComboReviewRevision.FindingDecisionExclude && string.IsNullOrWhiteSpace(fd.Reason))
                return Result.Failure(AppError.Validation("ComboReview.ExclusionReasonRequired", $"Excluding finding '{fd.FindingId}' requires an explicit reason."));
        }

        if (findingDecisionById.Count != findingsById.Count)
        {
            return Result.Failure(AppError.Validation("ComboReview.IncompleteFindings",
                "Every finding in the COMBO batch must have an explicit review decision."));
        }

        // 2. Blocking items: exact set match by composite key (SourceType, SourceId, ReasonCode)
        var blockerKeysExpected = persistedBlockers
            .Select(b => (b.SourceType.Trim(), b.SourceId.Trim(), b.ReasonCode.Trim()))
            .ToHashSet();

        var blockerResolutionByKey = new Dictionary<(string SourceType, string SourceId, string ReasonCode), ComboBlockerResolutionRecord>();
        foreach (var br in decisions.BlockerResolutions)
        {
            var key = (br.SourceType.Trim(), br.SourceId.Trim(), br.ReasonCode.Trim());
            if (!blockerKeysExpected.Contains(key))
            {
                return Result.Failure(AppError.Validation("ComboReview.UnknownBlocker",
                    $"Blocker resolution references unknown blocker '{key.Item1}:{key.Item2}:{key.Item3}'."));
            }

            if (!blockerResolutionByKey.TryAdd(key, br))
            {
                return Result.Failure(AppError.Validation("ComboReview.DuplicateBlockerResolution",
                    $"Duplicate resolution for blocker '{key.Item1}:{key.Item2}:{key.Item3}'."));
            }

            if (br.Resolution is not (
                SurveyComboReviewRevision.BlockerResolutionResolved or
                SurveyComboReviewRevision.BlockerResolutionAcceptedWithJustification or
                SurveyComboReviewRevision.BlockerResolutionExcluded or
                SurveyComboReviewRevision.BlockerResolutionCorrected or
                SurveyComboReviewRevision.BlockerResolutionUnresolved))
            {
                return Result.Failure(AppError.Validation("ComboReview.InvalidBlockerResolution", $"Unsupported blocker resolution '{br.Resolution}'."));
            }

            var isResolved = SurveyComboReviewRevision.IsResolvedBlockerStatus(br.Resolution);
            var mediaCheck = ValidateEvidenceMedia(br.EvidenceMediaIds, allowedMedia, required: isResolved);
            if (mediaCheck.IsFailure) return mediaCheck;

            if (isResolved && string.IsNullOrWhiteSpace(br.Reason))
                return Result.Failure(AppError.Validation("ComboReview.BlockerReasonRequired", $"Resolving blocker '{key.Item1}:{key.Item2}:{key.Item3}' requires a reason."));

            // Consistency check: if blocker is marked Excluded, any referenced finding in batch must be Excluded
            if (br.Resolution == SurveyComboReviewRevision.BlockerResolutionExcluded &&
                Guid.TryParse(key.Item2, out var blockedFindingId) &&
                findingDecisionById.TryGetValue(blockedFindingId, out var fd) &&
                fd.Decision != SurveyComboReviewRevision.FindingDecisionExclude)
            {
                return Result.Failure(AppError.Validation("ComboReview.InvalidBlockerExclusion",
                    $"Blocker '{key.Item2}' was marked Excluded while finding '{blockedFindingId}' remains Accepted."));
            }

            // If spatial/boundary blocker is marked AcceptedWithJustification/Resolved for an accepted finding, verify point is inside FarmBoundary
            if (isResolved &&
                br.Resolution != SurveyComboReviewRevision.BlockerResolutionExcluded &&
                key.Item1 is "BOUNDARY_EXCEPTION" or "FINDING_SPATIAL" &&
                Guid.TryParse(key.Item2, out var spatialFindingId) &&
                findingsById.TryGetValue(spatialFindingId, out var spatialFinding) &&
                findingDecisionById.TryGetValue(spatialFindingId, out var spatialFd) &&
                spatialFd.Decision == SurveyComboReviewRevision.FindingDecisionAccept)
            {
                var inside = await IsPointInsideBoundaryAsync(order.TenantId, order.FarmId, batch.FarmBoundaryVersionId,
                    spatialFinding.Position.Coordinates[0], spatialFinding.Position.Coordinates[1], cancellationToken);
                if (!inside)
                {
                    return Result.Failure(AppError.Validation("ComboReview.OutOfBoundaryCannotAccept",
                        $"Out-of-boundary finding '{spatialFindingId}' cannot be accepted without excluding evidence."));
                }
            }
        }

        if (!blockerResolutionByKey.Keys.ToHashSet().SetEquals(blockerKeysExpected))
        {
            return Result.Failure(AppError.Validation("ComboReview.IncompleteBlockers",
                "BlockerResolutions must address the exact set of persisted blocking items in this COMBO batch."));
        }

        // 3. Zones & Guidance: exact 1-to-1 coverage + NTS polygon + boundary covers + accepted members + sourced guidance
        var zonesById = payload.DiseaseZones.ToDictionary(z => z.SourceProposalId.Trim(), StringComparer.Ordinal);
        var zoneDecisionById = new Dictionary<string, ComboZoneReviewDecisionRecord>(StringComparer.Ordinal);
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        // Preload sourced guidance referenced by zones
        var referencedGuidanceIds = decisions.ZoneDecisions
            .Where(z => z.SelectedGuidanceId.HasValue)
            .Select(z => z.SelectedGuidanceId!.Value)
            .Distinct()
            .ToArray();

        var guidanceLookup = referencedGuidanceIds.Length > 0
            ? await db.Set<SourcedGuidance>()
                .FromSqlInterpolated($"SELECT *, xmin FROM plant.sourced_guidance WHERE id = ANY({referencedGuidanceIds}) FOR SHARE")
                .ToDictionaryAsync(g => g.Id, cancellationToken)
            : [];

        foreach (var zd in decisions.ZoneDecisions)
        {
            var proposalId = zd.SourceProposalId.Trim();
            if (!zonesById.TryGetValue(proposalId, out var proposal))
            {
                return Result.Failure(AppError.Validation("ComboReview.UnknownZone",
                    $"Zone decision references unknown SourceProposalId '{proposalId}'."));
            }

            if (!zoneDecisionById.TryAdd(proposalId, zd))
            {
                return Result.Failure(AppError.Validation("ComboReview.DuplicateZoneDecision",
                    $"Duplicate zone review decision for '{proposalId}'."));
            }

            if (zd.Decision is not (
                SurveyComboReviewRevision.ZoneDecisionApprove or
                SurveyComboReviewRevision.ZoneDecisionReject or
                SurveyComboReviewRevision.ZoneDecisionAdjust))
            {
                return Result.Failure(AppError.Validation("ComboReview.InvalidZoneDecision", $"Unsupported zone decision '{zd.Decision}'."));
            }

            var requiresJustification = zd.Decision is SurveyComboReviewRevision.ZoneDecisionReject or SurveyComboReviewRevision.ZoneDecisionAdjust;
            var mediaCheck = ValidateEvidenceMedia(zd.EvidenceMediaIds, allowedMedia, required: requiresJustification);
            if (mediaCheck.IsFailure) return mediaCheck;

            if (requiresJustification && string.IsNullOrWhiteSpace(zd.Reason))
                return Result.Failure(AppError.Validation("ComboReview.ZoneReasonRequired", $"Zone decision '{zd.Decision}' for '{proposalId}' requires a reason."));

            if (zd.Decision == SurveyComboReviewRevision.ZoneDecisionReject)
                continue;

            // Approve or Adjust: validate geometry, boundary coverage, accepted memberships, and sourced guidance
            var targetGeoJson = zd.ReviewedGeometry ?? proposal.Geometry;
            if (!TryBuildValidPolygon(targetGeoJson, geometryFactory, out var zonePolygon, out var geomError))
                return Result.Failure(AppError.Validation("ComboReview.InvalidZoneGeometry", geomError!));

            var zoneInsideBoundary = await IsPolygonInsideBoundaryAsync(
                order.TenantId, order.FarmId, batch.FarmBoundaryVersionId, zonePolygon!, cancellationToken);
            if (!zoneInsideBoundary)
            {
                return Result.Failure(AppError.Validation("ComboReview.ZoneOutOfBoundary",
                    $"Reviewed zone '{proposalId}' geometry must be covered by the capture farm boundary."));
            }

            var memberFindingIds = zd.ReviewedFindingIds ??
                proposal.Memberships.Select(m => m.FindingId).ToArray();

            if (zd.Decision == SurveyComboReviewRevision.ZoneDecisionAdjust &&
                zd.ReviewedGeometry is null &&
                zd.ReviewedFindingIds is null &&
                !zd.SelectedGuidanceId.HasValue)
            {
                return Result.Failure(AppError.Validation("ComboReview.AdjustRequiresChanges",
                    $"Zone decision 'Adjust' for '{proposalId}' requires reviewed geometry, finding memberships, or guidance selection."));
            }

            if (memberFindingIds.Count == 0 || memberFindingIds.Distinct().Count() != memberFindingIds.Count)
            {
                return Result.Failure(AppError.Validation("ComboReview.InvalidZoneMembers",
                    $"Approved zone '{proposalId}' must have a non-empty unique set of member findings."));
            }

            foreach (var fid in memberFindingIds)
            {
                if (!findingsById.TryGetValue(fid, out var memberFinding) ||
                    !findingDecisionById.TryGetValue(fid, out var memberDecision) ||
                    memberDecision.Decision != SurveyComboReviewRevision.FindingDecisionAccept)
                {
                    return Result.Failure(AppError.Validation("ComboReview.ZoneMemberNotAccepted",
                        $"Zone '{proposalId}' references finding '{fid}' which is missing or not accepted in this review."));
                }

                var findingPoint = geometryFactory.CreatePoint(
                    new Coordinate(memberFinding.Position.Coordinates[0], memberFinding.Position.Coordinates[1]));
                if (!zonePolygon!.Covers(findingPoint))
                {
                    return Result.Failure(AppError.Validation("ComboReview.MemberOutsideZoneGeometry",
                        $"Zone '{proposalId}' geometry does not cover accepted finding '{fid}'."));
                }
            }

            // Sourced guidance validation (per D03 & PlantHealthReviewService.Validation.cs)
            if (zd.SelectedGuidanceId is Guid guidanceId)
            {
                if (!guidanceLookup.TryGetValue(guidanceId, out var guidance) || !guidance.IsEffectiveAt(evaluatedAt))
                {
                    return Result.Failure(AppError.Validation("ComboReview.GuidanceNotApplicable",
                        $"Selected guidance '{guidanceId}' was not found or is not effective at evaluation time."));
                }

                if (!string.Equals(guidance.SuspectedClassCode.Trim(), proposal.SuspectedClassCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure(AppError.Validation("ComboReview.GuidanceClassMismatch",
                        $"Selected guidance '{guidanceId}' suspected class '{guidance.SuspectedClassCode}' does not match zone class '{proposal.SuspectedClassCode}'."));
                }

                if (zd.SelectedGuidanceVersionNumber.HasValue && guidance.VersionNumber != zd.SelectedGuidanceVersionNumber.Value)
                {
                    return Result.Failure(AppError.Validation("ComboReview.GuidanceVersionMismatch",
                        $"Selected guidance version '{zd.SelectedGuidanceVersionNumber}' does not match persisted guidance version '{guidance.VersionNumber}'."));
                }

                if (proposal.GuidanceCandidates.Count > 0 &&
                    !proposal.GuidanceCandidates.Any(gc =>
                        gc.GuidanceId == guidance.Id &&
                        gc.VersionNumber == guidance.VersionNumber &&
                        string.Equals(gc.Code, guidance.Code, StringComparison.Ordinal)))
                {
                    return Result.Failure(AppError.Validation("ComboReview.GuidanceNotInCandidates",
                        $"Selected guidance '{guidanceId}' (v{guidance.VersionNumber}) does not match pinned batch guidance candidates."));
                }
            }
            else if (proposal.GuidanceCandidates.Count > 0)
            {
                return Result.Failure(AppError.Validation("ComboReview.GuidanceSelectionRequired",
                    $"Zone '{proposalId}' has pinned sourced guidance candidates and requires an explicit SelectedGuidanceId."));
            }
        }

        if (!zoneDecisionById.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(zonesById.Keys))
        {
            return Result.Failure(AppError.Validation("ComboReview.IncompleteZones",
                "Every proposed disease zone in the COMBO batch must have an explicit review decision."));
        }

        // 4. Outcome Approved checks (blockers, coverage, negative confirmation)
        if (outcome == SurveyComboReviewRevision.OutcomeApproved)
        {
            var unresolved = blockerResolutionByKey.Values.Count(b => !SurveyComboReviewRevision.IsResolvedBlockerStatus(b.Resolution));
            if (unresolved > 0)
            {
                return Result.Failure(AppError.Conflict("ComboReview.UnresolvedBlockers",
                    $"Cannot approve COMBO review while {unresolved} unresolved blocking item(s) remain."));
            }

            var acceptedFindingsCount = findingDecisionById.Values.Count(f => f.Decision == SurveyComboReviewRevision.FindingDecisionAccept);
            if (acceptedFindingsCount == 0)
            {
                if (!job.HasCompleteComboInputCoverage())
                {
                    return Result.Failure(AppError.Conflict("ComboReview.InsufficientCoverageProof",
                        "Approving a negative COMBO result (zero accepted findings) requires complete per-image AI coverage proof."));
                }

                if (!confirmNoDiseaseObserved)
                {
                    return Result.Failure(AppError.Validation("ComboReview.NegativeConfirmationRequired",
                        "Approving a review with zero accepted disease findings requires ConfirmNoDiseaseObserved = true."));
                }

                if (findingsById.Count > 0 && string.IsNullOrWhiteSpace(negativeConfirmationEvidence))
                {
                    return Result.Failure(AppError.Validation("ComboReview.NegativeEvidenceRequired",
                        "Excluding all suspected findings requires explicit NegativeResultConfirmationEvidence to justify a negative assessment."));
                }
            }
        }

        return Result.Success();
    }

    private static Result ValidateEvidenceMedia(
        IReadOnlyList<Guid>? mediaIds,
        HashSet<Guid> allowedMedia,
        bool required)
    {
        if (required && (mediaIds is null || mediaIds.Count == 0))
            return Result.Failure(AppError.Validation("ComboReview.EvidenceRequired", "Supporting evidence media IDs are required for this decision."));

        if (mediaIds is null || mediaIds.Count == 0)
            return Result.Success();

        if (mediaIds.Count > 128 || mediaIds.Contains(Guid.Empty) || mediaIds.Distinct().Count() != mediaIds.Count)
            return Result.Failure(AppError.Validation("ComboReview.InvalidEvidence", "EvidenceMediaIds must contain unique non-empty GUIDs (max 128)."));

        if (mediaIds.Any(id => !allowedMedia.Contains(id)))
            return Result.Failure(AppError.Validation("ComboReview.ForeignEvidence", "Every evidence media reference must belong to the source Mission/Tenant/Farm."));

        return Result.Success();
    }

    private static bool TryBuildValidPolygon(
        GeoJsonPolygonV3? source,
        GeometryFactory factory,
        out Polygon? polygon,
        out string? error)
    {
        polygon = null;
        error = null;
        if (source?.Type != "Polygon" || source.Coordinates is not { Count: > 0 and <= 128 })
        {
            error = "A valid WGS84 GeoJSON Polygon is required.";
            return false;
        }

        var rings = new List<LinearRing>();
        foreach (var ring in source.Coordinates)
        {
            if (ring is not { Count: >= 4 and <= 10_000 })
            {
                error = "Polygon rings must be closed and bounded between 4 and 10,000 coordinates.";
                return false;
            }

            var coords = new Coordinate[ring.Count];
            for (int i = 0; i < ring.Count; i++)
            {
                var pt = ring[i];
                if (pt is not { Count: 2 } || !double.IsFinite(pt[0]) || !double.IsFinite(pt[1]) ||
                    pt[0] is < -180 or > 180 || pt[1] is < -90 or > 90)
                {
                    error = "Polygon contains invalid WGS84 coordinates.";
                    return false;
                }
                coords[i] = new Coordinate(pt[0], pt[1]);
            }

            if (!coords[0].Equals2D(coords[^1]))
            {
                error = "Polygon rings must be closed (first and last coordinate must match).";
                return false;
            }

            rings.Add(factory.CreateLinearRing(coords));
        }

        polygon = factory.CreatePolygon(rings[0], rings.Skip(1).ToArray());
        if (!polygon.IsValid || polygon.IsEmpty)
        {
            error = "Zone polygon topology is invalid or empty.";
            return false;
        }

        return true;
    }

    private async Task<bool> IsPointInsideBoundaryAsync(
        Guid tenantId,
        Guid farmId,
        Guid boundaryVersionId,
        double lon,
        double lat,
        CancellationToken cancellationToken)
    {
        var pt = new Point(lon, lat) { SRID = 4326 };
        return await db.Database.SqlQuery<bool>($"""
            SELECT COALESCE(ST_Covers(geometry, {pt}), FALSE) AS "Value"
            FROM farm.farm_boundaries
            WHERE id = {boundaryVersionId} AND tenant_id = {tenantId} AND farm_id = {farmId}
            """).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsPolygonInsideBoundaryAsync(
        Guid tenantId,
        Guid farmId,
        Guid boundaryVersionId,
        Polygon polygon,
        CancellationToken cancellationToken)
    {
        return await db.Database.SqlQuery<bool>($"""
            SELECT COALESCE(ST_Covers(geometry, {polygon}), FALSE) AS "Value"
            FROM farm.farm_boundaries
            WHERE id = {boundaryVersionId} AND tenant_id = {tenantId} AND farm_id = {farmId}
            """).SingleOrDefaultAsync(cancellationToken);
    }

    internal async Task<bool> IsAssignedManagerAsync(
        Guid tenantId,
        Guid farmId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return false;

        // Acquire rank-1 locks in ADR order. System managers may be assigned across tenants.
        if (await ReadAuthorizationValueAsync(
                "SELECT id FROM identity.tenants WHERE id = @tenant_id AND status = 'ACTIVE' FOR SHARE",
                cancellationToken) is not Guid ||
            await ReadAuthorizationValueAsync(
                "SELECT id FROM identity.users WHERE id = @actor_id AND status = 'ACTIVE' FOR SHARE",
                cancellationToken) is not Guid ||
            await ReadAuthorizationValueAsync(
                "SELECT status::text FROM identity.tenant_memberships WHERE tenant_id = @tenant_id AND user_id = @actor_id FOR SHARE",
                cancellationToken) is string membershipStatus && membershipStatus != "ACTIVE")
        {
            return false;
        }

        var profile = await ReadAuthorizationValueAsync(
            "SELECT id FROM identity.system_manager_profiles WHERE user_id = @actor_id AND status = 'ACTIVE' FOR SHARE",
            cancellationToken);
        if (profile is not Guid profileId ||
            await ReadAuthorizationValueAsync(
                "SELECT id FROM farm.farms WHERE id = @farm_id AND tenant_id = @tenant_id AND status = 'ACTIVE' FOR SHARE",
                cancellationToken) is not Guid)
        {
            return false;
        }

        return await ReadAuthorizationValueAsync(
            """
            SELECT id FROM identity.farm_manager_assignments
            WHERE tenant_id = @tenant_id AND farm_id = @farm_id
              AND system_manager_profile_id = @profile_id AND ended_at IS NULL
            FOR SHARE
            """, cancellationToken, profileId) is Guid;

        async Task<object?> ReadAuthorizationValueAsync(string sql, CancellationToken token, Guid? lockedProfileId = null)
        {
            await using var command = CreateCommand(sql);
            command.Parameters.Add(new NpgsqlParameter("tenant_id", tenantId));
            command.Parameters.Add(new NpgsqlParameter("actor_id", actorId));
            command.Parameters.Add(new NpgsqlParameter("farm_id", farmId));
            if (lockedProfileId.HasValue)
                command.Parameters.Add(new NpgsqlParameter("profile_id", lockedProfileId.Value));

            return await command.ExecuteScalarAsync(token);
        }
    }

    private DbCommand CreateCommand(string sql)
    {
        var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        return command;
    }

    private DateTimeOffset ComputeNormalizedUtcTimestamp(params DateTimeOffset?[] lowerBounds)
    {
        var raw = timeProvider.GetUtcNow().ToUniversalTime();
        foreach (var bound in lowerBounds)
        {
            if (bound.HasValue && bound.Value.ToUniversalTime() > raw)
                raw = bound.Value.ToUniversalTime();
        }

        var ticks = (raw.Ticks / 10L) * 10L;
        foreach (var bound in lowerBounds)
        {
            if (bound.HasValue && ticks < bound.Value.ToUniversalTime().Ticks)
            {
                ticks = ((bound.Value.ToUniversalTime().Ticks + 9L) / 10L) * 10L;
            }
        }

        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private Guid ResolveCorrelationId() =>
        executionContext.CorrelationId != Guid.Empty ? executionContext.CorrelationId : Guid.CreateVersion7();

    private static string ComputeRequestFingerprint<T>(T value) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, CanonicalJsonOptions)));

    private static bool IsConcurrencyOrLockError(PostgresException pg) =>
        pg.SqlState is PostgresErrorCodes.SerializationFailure
            or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.LockNotAvailable
            or PostgresErrorCodes.UniqueViolation;

    private static SurveyComboPendingReviewBatch MapPendingBatch(
        SurveyComboImportBatch batch,
        ComboAnalysisReadyV1 payload,
        List<ComboPendingBlockingItem> persistedItems) =>
        new(
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
            BlockingReasons: persistedItems.Select(i => $"{i.SourceType}:{i.SourceId}:{i.ReasonCode}").ToList(),
            Status: batch.Status,
            ResultNature: payload.ResultNature,
            LimitationNotice: payload.LimitationNotice,
            Findings: payload.Findings.Select(f => new ComboPendingFindingDto(
                f.FindingId,
                f.FindingVersion,
                f.PlantProfileId,
                f.MappingCandidateId,
                f.Position.Coordinates[0],
                f.Position.Coordinates[1],
                f.ObservedAt,
                f.SuspectedClassCode,
                f.ClassificationNature,
                f.Confidence,
                f.Uncertainty,
                f.Limitations,
                f.EvidenceMedia.Select(m => m.MediaAssetId).ToList())).ToList(),
            BoundaryExceptions: payload.BoundaryExceptions.Select(e => new ComboPendingBoundaryExceptionDto(
                e.SourceReferenceId,
                e.OriginalPosition.Coordinates[0],
                e.OriginalPosition.Coordinates[1],
                e.BoundaryClassification,
                e.MeasuredBoundaryDistanceM,
                e.BoundaryThresholdM,
                e.EvidenceMedia.Select(m => m.MediaAssetId).ToList())).ToList(),
            DiseaseZones: payload.DiseaseZones.Select(z => new ComboPendingDiseaseZoneDto(
                z.SourceProposalId,
                z.Geometry,
                z.SuspectedClassCode,
                z.MembershipVersion,
                z.Memberships.Select(m => new ComboPendingZoneMembershipDto(
                    m.FindingId,
                    m.PlantProfileId,
                    m.MappingCandidateId,
                    m.Confidence)).ToList(),
                z.GuidanceCandidates.Select(g => new ComboPendingGuidanceCandidateDto(
                    g.GuidanceId,
                    g.VersionNumber,
                    g.Code,
                    g.SourceReference)).ToList(),
                z.EvidenceMedia.Select(m => m.MediaAssetId).ToList())).ToList());

    private static AppError MapPersistenceException(string operation, Exception ex)
    {
        if (ex is SurveyDomainException domainEx)
            return AppError.Conflict($"{operation}.{domainEx.Code}", domainEx.Message);

        return AppError.Conflict($"{operation}.ConcurrencyConflict",
            $"Concurrent state change or database constraint conflict occurred during {operation}: {ex.GetBaseException().Message}");
    }
}
