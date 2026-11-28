using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.CorrectSurveyOrderPreReleaseInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.FinalizeSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using Npgsql;
using SurveyScopeType = AgriDrone.Modules.Surveys.Domain.SurveyScopeType;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyInvoiceTransaction(
    AgriDroneSchemaDbContext db,
    IExecutionContext executionContext,
    OutboxMessageFactory outboxFactory,
    TimeProvider timeProvider,
    SurveyResultReviewTransaction resultReviewTransaction) : ISurveyInvoiceTransaction
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

    private sealed record ComboEvidenceVerification(
        SurveyComboImportBatch Batch,
        ComboAnalysisReadyV1 Payload,
        SurveyComboReviewRevision Review,
        ComboReviewDecisionsSnapshotRecord Decisions);

    private sealed record VerifiedInvoiceContext(
        SurveyOrder Order,
        SurveyOrderCommercialSnapshot Commercial,
        SurveyInvoiceFundingSummary Funding,
        IReadOnlyList<SurveyPayment> OpenFinalBalanceCheckouts,
        SurveyInvoiceRevision? CurrentInvoice,
        SurveyInternalApprovedResultRevision ApprovedResult,
        SurveyOrderPoleCountSnapshot CountSnapshot,
        MappingCandidateBatch MappingBatch,
        FarmBaseMapVersion BaseMap,
        FarmBoundary Boundary,
        CaptureAttempt Attempt,
        DroneMission Mission,
        ComboEvidenceVerification? ComboEvidence,
        Guid BaselineMapVersionId);

    public async Task<Result<FinalizeSurveyOrderInvoiceResponse>> FinalizeInvoiceAsync(
        FinalizeSurveyOrderInvoiceCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Validation("Command.Required", "Command payload is required."));
        if (actorId == Guid.Empty)
            return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Unauthorized("InvoiceFinalization.Unauthorized", "Valid actor is required."));

        var reason = command.FinalizationReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Validation("FinalizationReason.Invalid", "FinalizationReason must be between 1 and 500 characters."));

        var expectedApprovalFp = command.ExpectedApprovedResultFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        var expectedCountFp = command.ExpectedCountFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Hex64Regex.IsMatch(expectedApprovalFp) || !Hex64Regex.IsMatch(expectedCountFp))
            return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Validation("ExpectedFingerprint.Invalid", "Expected fingerprints must be 64-character lowercase hex strings."));

        var normalizedZeroPoleReason = string.IsNullOrWhiteSpace(command.ZeroPoleReasonCode) ? null : command.ZeroPoleReasonCode.Trim();
        var normalizedZeroPoleEvidence = string.IsNullOrWhiteSpace(command.ZeroPoleEvidenceReference) ? null : command.ZeroPoleEvidenceReference.Trim();

        var requestFingerprint = ComputeRequestFingerprint(new
        {
            command.OrderId,
            Action = SurveyResultReviewOperation.ActionFinalizeInvoice,
            command.ExpectedOrderVersion,
            command.ExpectedApprovedResultRevisionId,
            ExpectedApprovedResultFingerprint = expectedApprovalFp,
            command.ExpectedCountSnapshotId,
            ExpectedCountFingerprint = expectedCountFp,
            FinalizationReason = reason,
            ZeroPoleReasonCode = normalizedZeroPoleReason,
            ZeroPoleEvidenceReference = normalizedZeroPoleEvidence
        });
        Polygon? plantHealthSurveyArea = null;
        if (command.PlantHealthSurveyArea is not null)
        {
            if (!SurveyResultReviewTransaction.TryBuildValidPolygon(command.PlantHealthSurveyArea,
                    new GeometryFactory(new PrecisionModel(), 4326), out plantHealthSurveyArea, out var error))
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Validation("ComboPricing.InvalidSurveyArea", error!));

            plantHealthSurveyArea!.Normalize();
            requestFingerprint = ComputeRequestFingerprint(new { requestFingerprint, command.PlantHealthSurveyArea });
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var scope = await LoadOrderScopeAsync(command.OrderId, cancellationToken);
            if (scope is null)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

            // ADR 0020 Rank 1: Authorize operator/approver BEFORE Order Rank 2 and BEFORE receipt Rank 3
            if (!await IsAssignedManagerAsync(scope.TenantId, scope.FarmId, actorId, cancellationToken))
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Forbidden("InvoiceFinalization.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            // ADR 0020 Rank 2: Lock Order(s) FOR UPDATE
            var order = await LockRelatedOrdersAndGetCurrentAsync(scope, cancellationToken);
            if (order.TenantId != scope.TenantId || order.FarmId != scope.FarmId)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Forbidden("InvoiceFinalization.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            // ADR 0020 Rank 3: Replay check BEFORE mutable state / version checks
            var replayResult = await TryLoadReplayAsync<FinalizeSurveyOrderInvoiceResponse>(
                order.TenantId,
                order.Id,
                actorId,
                SurveyResultReviewOperation.ActionFinalizeInvoice,
                command.OperationId,
                requestFingerprint,
                cancellationToken);
            if (replayResult is not null)
            {
                if (replayResult.IsFailure)
                    return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(replayResult.Error);
                await tx.CommitAsync(cancellationToken);
                return Result.Success(replayResult.Value with { IsReplay = true });
            }

            if (order.Status != SurveyOrderStatus.Processing)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Conflict("SurveyOrder.StatusInvalid", $"Order must be in Processing status for initial invoice finalization (current: {order.Status})."));

            if (order.CurrentInvoiceRevisionId.HasValue)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Conflict("InvoiceFinalization.AlreadyFinalized", "An active invoice revision already exists; use pre-release correction instead."));

            if (order.Version != command.ExpectedOrderVersion)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Conflict("SurveyOrder.VersionConflict", $"Expected order version {command.ExpectedOrderVersion}, actual {order.Version}."));

            var contextResult = await LockAndValidateOrderedContextAsync(
                order,
                isCorrection: false,
                cancellationToken);
            if (contextResult.IsFailure)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(contextResult.Error);

            var ctx = contextResult.Value;

            if (ctx.ApprovedResult.Id != command.ExpectedApprovedResultRevisionId ||
                !string.Equals(ctx.ApprovedResult.EvidenceFingerprint, expectedApprovalFp, StringComparison.Ordinal))
            {
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Conflict("ApprovedResult.Stale", "Expected approved result revision ID or fingerprint does not match current order approval."));
            }

            if (ctx.CountSnapshot.Id != command.ExpectedCountSnapshotId ||
                !string.Equals(ctx.CountSnapshot.EvidenceFingerprint, expectedCountFp, StringComparison.Ordinal))
            {
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(AppError.Conflict("CountSnapshot.Stale", "Expected count snapshot ID or fingerprint does not match current order count snapshot."));
            }

            // R05E-05: Direct server-derived approver from authenticated Rank-1 actor
            var zeroPoleCheck = ValidateZeroPoleDecision(
                ctx.CountSnapshot.VerifiedBillablePoleCount,
                actorId,
                normalizedZeroPoleReason,
                normalizedZeroPoleEvidence,
                "InvoiceFinalization");
            if (zeroPoleCheck.IsFailure)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(zeroPoleCheck.Error);

            var zeroPoleApprovedBy = zeroPoleCheck.Value;

            var finalizedAt = ComputeNormalizedUtcTimestamp(
                order.UpdatedAt,
                ctx.ApprovedResult.ReviewedAt,
                ctx.CountSnapshot.VerifiedAt);

            var invoiceId = Guid.CreateVersion7();
            var unitPrice = ctx.Commercial.PricePerPole;
            var pricing = await ResolveComboPricingAsync(ctx, ctx.CountSnapshot.Items, cancellationToken, plantHealthSurveyArea);
            if (pricing.IsFailure)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(pricing.Error);
            var total = SurveyInvoiceRevision.CalculateTotal(ctx.CountSnapshot.VerifiedBillablePoleCount, unitPrice, pricing.Value);
            if (total.IsFailure)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(total.Error);
            var totalAmount = total.Value;
            var dueAmount = Math.Max(0m, totalAmount - ctx.Funding.ValidAllocatedDepositNet);
            var overpaymentAmount = Math.Max(0m, ctx.Funding.ValidAllocatedDepositNet - totalAmount);

            var invoiceFingerprint = SurveyInvoiceRevision.ComputeEvidenceFingerprint(
                tenantId: order.TenantId,
                surveyOrderId: order.Id,
                farmId: order.FarmId,
                revisionNumber: 1,
                approvedResultRevisionId: ctx.ApprovedResult.Id,
                poleCountSnapshotId: ctx.CountSnapshot.Id,
                verifiedBillablePoleCount: ctx.CountSnapshot.VerifiedBillablePoleCount,
                surveyServicePriceId: order.SurveyServicePriceId!.Value,
                pricePerPole: unitPrice,
                currency: ctx.Commercial.Currency,
                commercialPolicyVersionId: order.BusinessPolicyVersionId!.Value,
                totalAmount: totalAmount,
                validAllocatedDepositNet: ctx.Funding.ValidAllocatedDepositNet,
                dueAmount: dueAmount,
                overpaymentAmount: overpaymentAmount,
                finalizedBy: actorId,
                finalizedAt: finalizedAt,
                finalizationReason: reason,
                predecessorRevisionId: null,
                zeroPoleApprovedBy: zeroPoleApprovedBy,
                zeroPoleReasonCode: normalizedZeroPoleReason,
                zeroPoleEvidenceReference: normalizedZeroPoleEvidence,
                comboPricing: pricing.Value);

            var invoiceResult = SurveyInvoiceRevision.Create(
                id: invoiceId,
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                revisionNumber: 1,
                approvedResultRevisionId: ctx.ApprovedResult.Id,
                poleCountSnapshotId: ctx.CountSnapshot.Id,
                verifiedBillablePoleCount: ctx.CountSnapshot.VerifiedBillablePoleCount,
                surveyServicePriceId: order.SurveyServicePriceId.Value,
                pricePerPole: unitPrice,
                currency: ctx.Commercial.Currency,
                commercialPolicyVersionId: order.BusinessPolicyVersionId.Value,
                validAllocatedDepositNet: ctx.Funding.ValidAllocatedDepositNet,
                finalizedBy: actorId,
                finalizedAt: finalizedAt,
                finalizationReason: reason,
                evidenceFingerprint: invoiceFingerprint,
                predecessorRevisionId: null,
                zeroPoleApprovedBy: zeroPoleApprovedBy,
                zeroPoleReasonCode: normalizedZeroPoleReason,
                zeroPoleEvidenceReference: normalizedZeroPoleEvidence,
                comboPricing: pricing.Value);

            if (invoiceResult.IsFailure)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(invoiceResult.Error);

            var invoice = invoiceResult.Value;
            db.Set<SurveyInvoiceRevision>().Add(invoice);
            await db.SaveChangesAsync(cancellationToken);

            var previousStatus = order.Status;
            var finalizeOrderResult = order.FinalizeInvoiceRevision(invoice, currentInvoiceRevision: null, command.ExpectedOrderVersion);
            if (finalizeOrderResult.IsFailure)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(finalizeOrderResult.Error);

            // PostgreSQL assigns the Order version used by history, the checkout handoff, and release.
            await db.SaveChangesAsync(cancellationToken);
            var correlationId = ResolveCorrelationId();
            if (previousStatus != order.Status)
            {
                await AppendOrderStatusHistoryAsync(
                    order,
                    previousStatus,
                    actorId,
                    "V3_INVOICE_FINALIZATION",
                    reason,
                    invoice.Id.ToString("D"),
                    correlationId,
                    finalizedAt,
                    cancellationToken);
            }

            var response = new FinalizeSurveyOrderInvoiceResponse(
                OrderId: order.Id,
                OrderVersion: order.Version,
                OrderStatus: order.Status.ToString(),
                ServiceCode: ctx.ApprovedResult.ServiceCode,
                ScreensDisease: ctx.ApprovedResult.ScreensDisease,
                InvoiceRevisionId: invoice.Id,
                InvoiceRevisionNumber: invoice.RevisionNumber,
                InvoiceFingerprint: invoice.EvidenceFingerprint,
                ApprovedResultRevisionId: ctx.ApprovedResult.Id,
                ApprovedResultFingerprint: ctx.ApprovedResult.EvidenceFingerprint,
                CountSnapshotId: ctx.CountSnapshot.Id,
                CountSnapshotFingerprint: ctx.CountSnapshot.EvidenceFingerprint,
                VerifiedBillablePoleCount: invoice.VerifiedBillablePoleCount,
                SurveyServicePriceId: invoice.SurveyServicePriceId,
                PricePerPole: invoice.PricePerPole,
                Currency: invoice.Currency,
                CommercialPolicyVersionId: invoice.CommercialPolicyVersionId,
                TotalAmount: invoice.TotalAmount,
                ValidAllocatedDepositNet: invoice.ValidAllocatedDepositNet,
                ConfirmedNetReceipts: ctx.Funding.ConfirmedNetReceipts,
                DueAmount: invoice.DueAmount,
                OverpaymentAmount: invoice.OverpaymentAmount,
                ZeroPoleApprovedBy: invoice.ZeroPoleApprovedBy,
                ZeroPoleReasonCode: invoice.ZeroPoleReasonCode,
                ZeroPoleEvidenceReference: invoice.ZeroPoleEvidenceReference,
                FinalizedAt: finalizedAt,
                IsReplay: false);

            var outcomeDoc = JsonSerializer.SerializeToDocument(response, CanonicalJsonOptions);
            var opResult = SurveyResultReviewOperation.Create(
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                actorId: actorId,
                action: SurveyResultReviewOperation.ActionFinalizeInvoice,
                apiVersion: SurveyResultReviewOperation.ApiVersionV3,
                operationId: command.OperationId,
                requestFingerprint: requestFingerprint,
                outcome: outcomeDoc,
                createdAt: finalizedAt,
                comboBatchId: ctx.ApprovedResult.ComboBatchId,
                reviewRevisionId: ctx.ApprovedResult.ComboReviewRevisionId,
                approvedResultRevisionId: ctx.ApprovedResult.Id,
                captureAttemptId: ctx.Attempt.Id,
                poleCountSnapshotId: ctx.CountSnapshot.Id,
                invoiceRevisionId: invoice.Id);

            if (opResult.IsFailure)
                return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(opResult.Error);

            db.Set<SurveyResultReviewOperation>().Add(opResult.Value);

            using var auditDoc = JsonSerializer.SerializeToDocument(new
            {
                invoice.Id,
                invoice.RevisionNumber,
                invoice.ApprovedResultRevisionId,
                invoice.PoleCountSnapshotId,
                invoice.VerifiedBillablePoleCount,
                invoice.TotalAmount,
                invoice.ValidAllocatedDepositNet,
                ctx.Funding.ConfirmedNetReceipts,
                invoice.DueAmount,
                invoice.OverpaymentAmount,
                invoice.EvidenceFingerprint,
                invoice.ZeroPoleApprovedBy,
                invoice.ZeroPoleReasonCode
            });
            db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                order.TenantId,
                order.FarmId,
                actorId,
                correlationId,
                nameof(SurveyInvoiceRevision),
                invoice.Id,
                "INVOICE_FINALIZED",
                oldData: null,
                newData: auditDoc,
                createdAt: finalizedAt));

            AddStateChangedOutbox(order, actorId, correlationId, "INVOICE_FINALIZED", finalizedAt, ctx.Attempt.Id, ctx.Mission.Id);

            AddReleaseEvaluationOutbox(
                order, invoice, ctx.ApprovedResult, ctx.CountSnapshot, SurveyReleaseTriggerSource.InvoiceFinalization,
                command.OperationId, requestFingerprint, actorId, correlationId, finalizedAt);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result.Success(response);
        }
        catch (Fault f)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(f.Error);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException or PostgresException or SurveyDomainException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<FinalizeSurveyOrderInvoiceResponse>(MapPersistenceException("InvoiceFinalization", ex));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<Result<CorrectSurveyOrderPreReleaseInvoiceResponse>> CorrectPreReleaseInvoiceAsync(
        CorrectSurveyOrderPreReleaseInvoiceCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (command is null)
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Validation("Command.Required", "Command payload is required."));
        if (actorId == Guid.Empty)
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Unauthorized("InvoiceCorrection.Unauthorized", "Valid actor is required."));

        var reason = command.CorrectionReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Validation("CorrectionReason.Invalid", "CorrectionReason must be between 1 and 500 characters."));

        if (command.ItemCorrections is not { Count: > 0 and <= 10_000 })
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Validation("ItemCorrections.Invalid", "ItemCorrections must contain between 1 and 10,000 items."));

        var expectedApprovalFp = command.ExpectedCurrentApprovedResultFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        var expectedInvoiceFp = command.ExpectedCurrentInvoiceFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        var expectedCountFp = command.ExpectedCurrentCountFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Hex64Regex.IsMatch(expectedApprovalFp) || !Hex64Regex.IsMatch(expectedInvoiceFp) || !Hex64Regex.IsMatch(expectedCountFp))
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Validation("ExpectedFingerprint.Invalid", "Expected fingerprints must be 64-character lowercase hex strings."));

        var canonicalDecision = SurveyInternalApprovedResultRevision.CanonicalizeReviewDecision(command.ReviewDecision);
        var normalizedZeroPoleReason = string.IsNullOrWhiteSpace(command.ZeroPoleReasonCode) ? null : command.ZeroPoleReasonCode.Trim();
        var normalizedZeroPoleEvidence = string.IsNullOrWhiteSpace(command.ZeroPoleEvidenceReference) ? null : command.ZeroPoleEvidenceReference.Trim();

        var orderedCorrections = command.ItemCorrections
            .OrderBy(i => i.MappingCandidateId)
            .Select(i => new PreReleasePoleCountItemCorrection(
                i.MappingCandidateId,
                i.IsAccepted,
                string.IsNullOrWhiteSpace(i.ExclusionReason) ? null : i.ExclusionReason.Trim(),
                i.ExpectedPlantProfileId))
            .ToList();

        if (orderedCorrections.Select(i => i.MappingCandidateId).Distinct().Count() != orderedCorrections.Count)
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Validation("ItemCorrections.Duplicate", "ItemCorrections cannot contain duplicate MappingCandidateId values."));

        var replacement = command.ReplacementComboReview;
        if (replacement is not null)
        {
            if (replacement.Decisions is not
                { FindingDecisions: not null, ZoneDecisions: not null, BlockerResolutions: not null } ||
                replacement.Decisions.FindingDecisions.Any(d => d is null || string.IsNullOrWhiteSpace(d.Decision)) ||
                replacement.Decisions.ZoneDecisions.Any(d => d is null || string.IsNullOrWhiteSpace(d.SourceProposalId) ||
                    string.IsNullOrWhiteSpace(d.Decision)) ||
                replacement.Decisions.BlockerResolutions.Any(d => d is null || string.IsNullOrWhiteSpace(d.SourceType) ||
                    string.IsNullOrWhiteSpace(d.SourceId) || string.IsNullOrWhiteSpace(d.ReasonCode) ||
                    string.IsNullOrWhiteSpace(d.Resolution)))
            {
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(
                    AppError.Validation("ComboReview.InvalidDecisions", "Replacement COMBO review decisions must have complete identifiers and decision values."));
            }

            replacement = replacement with
            {
                Decisions = SurveyComboReviewRevision.CanonicalizeDecisions(replacement.Decisions),
                NegativeResultConfirmationEvidence = string.IsNullOrWhiteSpace(replacement.NegativeResultConfirmationEvidence)
                    ? null
                    : replacement.NegativeResultConfirmationEvidence.Trim()
            };
        }

        var requestFingerprint = ComputeRequestFingerprint(new
        {
            command.OrderId,
            Action = SurveyResultReviewOperation.ActionCorrectPreReleaseInvoice,
            command.ExpectedOrderVersion,
            command.ExpectedCurrentApprovedResultRevisionId,
            ExpectedCurrentApprovedResultFingerprint = expectedApprovalFp,
            command.ExpectedCurrentInvoiceRevisionId,
            ExpectedCurrentInvoiceFingerprint = expectedInvoiceFp,
            command.ExpectedCurrentCountSnapshotId,
            ExpectedCurrentCountFingerprint = expectedCountFp,
            ReviewDecision = canonicalDecision,
            CorrectionReason = reason,
            ItemCorrections = orderedCorrections,
            ZeroPoleReasonCode = normalizedZeroPoleReason,
            ZeroPoleEvidenceReference = normalizedZeroPoleEvidence
        });

        if (replacement is not null)
            requestFingerprint = ComputeRequestFingerprint(new { requestFingerprint, ReplacementComboReview = replacement });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var scope = await LoadOrderScopeAsync(command.OrderId, cancellationToken);
            if (scope is null)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

            // ADR 0020 Rank 1: Authorize operator/approver BEFORE Order Rank 2 and BEFORE receipt Rank 3
            if (!await IsAssignedManagerAsync(scope.TenantId, scope.FarmId, actorId, cancellationToken))
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Forbidden("InvoiceCorrection.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            // ADR 0020 Rank 2: Lock Order(s) FOR UPDATE
            var order = await LockRelatedOrdersAndGetCurrentAsync(scope, cancellationToken);
            if (order.TenantId != scope.TenantId || order.FarmId != scope.FarmId)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Forbidden("InvoiceCorrection.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            // ADR 0020 Rank 3: Replay check BEFORE mutable state / version checks
            var replayResult = await TryLoadReplayAsync<CorrectSurveyOrderPreReleaseInvoiceResponse>(
                order.TenantId,
                order.Id,
                actorId,
                SurveyResultReviewOperation.ActionCorrectPreReleaseInvoice,
                command.OperationId,
                requestFingerprint,
                cancellationToken);
            if (replayResult is not null)
            {
                if (replayResult.IsFailure)
                    return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(replayResult.Error);
                await tx.CommitAsync(cancellationToken);
                return Result.Success(replayResult.Value with { IsReplay = true });
            }

            if (order.Status != SurveyOrderStatus.AwaitingSettlement)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Conflict("SurveyOrder.StatusInvalid", $"Order must be in AwaitingSettlement status for pre-release correction (current: {order.Status})."));

            if (!order.CurrentInvoiceRevisionId.HasValue)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Conflict("InvoiceCorrection.NoCurrentInvoice", "Order does not have an active invoice revision to correct."));

            if (order.Version != command.ExpectedOrderVersion)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Conflict("SurveyOrder.VersionConflict", $"Expected order version {command.ExpectedOrderVersion}, actual {order.Version}."));

            var contextResult = await LockAndValidateOrderedContextAsync(
                order,
                isCorrection: true,
                cancellationToken);
            if (contextResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(contextResult.Error);

            var ctx = contextResult.Value;
            var currentInvoice = ctx.CurrentInvoice!;

            if (ctx.ApprovedResult.Id != command.ExpectedCurrentApprovedResultRevisionId ||
                !string.Equals(ctx.ApprovedResult.EvidenceFingerprint, expectedApprovalFp, StringComparison.Ordinal))
            {
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Conflict("ApprovedResult.Stale", "Expected current approved result revision ID or fingerprint is stale."));
            }

            if (ctx.CountSnapshot.Id != command.ExpectedCurrentCountSnapshotId ||
                !string.Equals(ctx.CountSnapshot.EvidenceFingerprint, expectedCountFp, StringComparison.Ordinal))
            {
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Conflict("CountSnapshot.Stale", "Expected current count snapshot ID or fingerprint is stale."));
            }

            if (currentInvoice.Id != command.ExpectedCurrentInvoiceRevisionId ||
                !string.Equals(currentInvoice.EvidenceFingerprint, expectedInvoiceFp, StringComparison.Ordinal))
            {
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Conflict("InvoiceRevision.Stale", "Expected current invoice revision ID or fingerprint is stale."));
            }

            if (ctx.CountSnapshot.RevisionNumber == int.MaxValue ||
                ctx.ApprovedResult.RevisionNumber == int.MaxValue ||
                currentInvoice.RevisionNumber == int.MaxValue)
            {
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Conflict("Revision.LimitReached", "Revision sequence has reached maximum integer limit."));
            }

            // R05E-04: Build corrected snapshot items strictly from mapping-reviewed candidate/profile provenance
            var newSnapshotId = Guid.CreateVersion7();
            var buildItemsResult = await BuildCorrectedSnapshotItemsAsync(
                order,
                ctx.CountSnapshot,
                ctx.MappingBatch,
                ctx.Boundary,
                newSnapshotId,
                orderedCorrections,
                cancellationToken);
            if (buildItemsResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(buildItemsResult.Error);

            var newItems = buildItemsResult.Value;
            var newBillableCount = newItems
                .Where(x => x.IsAccepted && x.IsStrictlyInBoundary && x.PlantProfileId.HasValue)
                .Select(x => x.PlantProfileId!.Value)
                .Distinct()
                .Count();

            var pricing = await ResolveComboPricingAsync(ctx, newItems, cancellationToken);
            if (pricing.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(pricing.Error);

            if (newBillableCount == ctx.CountSnapshot.VerifiedBillablePoleCount &&
                pricing.Value?.PlantHealthPoleCount == currentInvoice.PlantHealthPoleCount)
            {
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(AppError.Conflict(
                    "InvoiceCorrection.CountUnchanged",
                    "Pre-release count correction requires a change to the verified map or plant health pole count."));
            }

            if (ctx.ComboEvidence is null && replacement is not null)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(
                    AppError.Validation("ComboReview.NotApplicable", "Only a COMBO correction can include replacement review decisions."));

            // R05E-05: Direct server-derived approver from authenticated Rank-1 actor
            var zeroPoleCheck = ValidateZeroPoleDecision(
                newBillableCount,
                actorId,
                normalizedZeroPoleReason,
                normalizedZeroPoleEvidence,
                "InvoiceCorrection");
            if (zeroPoleCheck.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(zeroPoleCheck.Error);

            var zeroPoleApprovedBy = zeroPoleCheck.Value;

            var maxCheckoutUpdatedAt = ctx.OpenFinalBalanceCheckouts.Count > 0
                ? ctx.OpenFinalBalanceCheckouts.Max(p => p.UpdatedAt)
                : (DateTimeOffset?)null;

            var correctedAt = ComputeNormalizedUtcTimestamp(
                order.UpdatedAt,
                ctx.ApprovedResult.ReviewedAt,
                currentInvoice.FinalizedAt,
                ctx.CountSnapshot.VerifiedAt,
                maxCheckoutUpdatedAt,
                ctx.ComboEvidence?.Review.ReviewedAt);

            var newSnapshotFp = SurveyOrderPoleCountSnapshot.ComputeEvidenceFingerprint(
                order.TenantId,
                order.Id,
                order.FarmId,
                ctx.CountSnapshot.MissionId,
                ctx.CountSnapshot.CaptureAttemptId,
                ctx.CountSnapshot.FarmBaseMapVersionId,
                ctx.CountSnapshot.FarmBoundaryVersionId,
                ctx.CountSnapshot.MappingCandidateBatchId,
                ctx.MappingBatch.SpatialPolicyFingerprint,
                newItems);

            var newSnapshot = SurveyOrderPoleCountSnapshot.Create(
                id: newSnapshotId,
                surveyOrderId: order.Id,
                tenantId: order.TenantId,
                farmId: order.FarmId,
                missionId: ctx.CountSnapshot.MissionId,
                captureAttemptId: ctx.CountSnapshot.CaptureAttemptId,
                farmBaseMapVersionId: ctx.CountSnapshot.FarmBaseMapVersionId,
                farmBoundaryVersionId: ctx.CountSnapshot.FarmBoundaryVersionId,
                mappingCandidateBatchId: ctx.CountSnapshot.MappingCandidateBatchId,
                spatialPolicyVersionId: ctx.CountSnapshot.SpatialPolicyVersionId,
                evidenceFingerprint: newSnapshotFp,
                items: newItems,
                verifiedBy: actorId,
                verifiedAt: correctedAt,
                revisionNumber: ctx.CountSnapshot.RevisionNumber + 1,
                predecessorSnapshotId: ctx.CountSnapshot.Id);

            var newApprovedId = Guid.CreateVersion7();
            SurveyComboReviewRevision? replacementReview = null;
            if (ctx.ComboEvidence is { } combo)
            {
                var decisions = replacement?.Decisions ?? combo.Decisions;
                var comboCheck = VerifyComboReviewAgainstCorrectedItems(combo.Payload, decisions, newItems);
                if (comboCheck.IsFailure)
                    return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(comboCheck.Error);

                if (replacement is not null)
                {
                    var reviewResult = await CreateReplacementComboReviewAsync(
                        order, combo, replacement, actorId, reason, correctedAt, cancellationToken);
                    if (reviewResult.IsFailure)
                        return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(reviewResult.Error);
                    replacementReview = reviewResult.Value;
                }
            }

            var effectiveReview = replacementReview ?? ctx.ComboEvidence?.Review;
            var healthStatus = effectiveReview is null
                ? ctx.ApprovedResult.HealthAssessmentStatus
                : effectiveReview.AcceptedFindingsCount > 0
                    ? SurveyInternalApprovedResultRevision.HealthSuspectedObservationsApproved
                    : SurveyInternalApprovedResultRevision.HealthNoObservationsDetected;

            var newApprovedRevisionNumber = ctx.ApprovedResult.RevisionNumber + 1;
            var newApprovedFp = SurveyInternalApprovedResultRevision.ComputeEvidenceFingerprint(
                tenantId: order.TenantId,
                surveyOrderId: order.Id,
                farmId: order.FarmId,
                revisionNumber: newApprovedRevisionNumber,
                serviceCode: ctx.ApprovedResult.ServiceCode,
                screensDisease: ctx.ApprovedResult.ScreensDisease,
                captureAttemptId: ctx.ApprovedResult.CaptureAttemptId,
                missionId: ctx.ApprovedResult.MissionId,
                farmBaseMapVersionId: ctx.ApprovedResult.FarmBaseMapVersionId,
                baselineMapVersionId: ctx.BaselineMapVersionId,
                farmBoundaryVersionId: ctx.ApprovedResult.FarmBoundaryVersionId,
                poleCountSnapshotId: newSnapshot.Id,
                mappingCandidateBatchId: ctx.ApprovedResult.MappingCandidateBatchId,
                comboBatchId: ctx.ApprovedResult.ComboBatchId,
                reviewDecision: canonicalDecision,
                healthAssessmentStatus: healthStatus,
                limitationNotice: ctx.ApprovedResult.LimitationNotice,
                reviewedBy: actorId,
                reviewedAt: correctedAt,
                reviewReason: reason,
                predecessorRevisionId: ctx.ApprovedResult.Id,
                comboReviewRevisionId: effectiveReview?.Id,
                comboReviewFingerprint: effectiveReview?.DecisionFingerprint);

            var newApprovedResult = SurveyInternalApprovedResultRevision.Create(
                id: newApprovedId,
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                revisionNumber: newApprovedRevisionNumber,
                serviceCode: ctx.ApprovedResult.ServiceCode,
                screensDisease: ctx.ApprovedResult.ScreensDisease,
                captureAttemptId: ctx.ApprovedResult.CaptureAttemptId,
                missionId: ctx.ApprovedResult.MissionId,
                farmBaseMapVersionId: ctx.ApprovedResult.FarmBaseMapVersionId,
                baselineMapVersionId: ctx.BaselineMapVersionId,
                farmBoundaryVersionId: ctx.ApprovedResult.FarmBoundaryVersionId,
                poleCountSnapshotId: newSnapshot.Id,
                mappingCandidateBatchId: ctx.ApprovedResult.MappingCandidateBatchId,
                comboBatchId: ctx.ApprovedResult.ComboBatchId,
                reviewDecision: canonicalDecision,
                healthAssessmentStatus: healthStatus,
                limitationNotice: ctx.ApprovedResult.LimitationNotice,
                reviewedBy: actorId,
                reviewedAt: correctedAt,
                reviewReason: reason,
                evidenceFingerprint: newApprovedFp,
                predecessorRevisionId: ctx.ApprovedResult.Id,
                comboReviewRevisionId: effectiveReview?.Id,
                comboReviewFingerprint: effectiveReview?.DecisionFingerprint);

            if (newApprovedResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(newApprovedResult.Error);

            var newApproved = newApprovedResult.Value;

            var newInvoiceId = Guid.CreateVersion7();
            var newInvoiceRevisionNumber = currentInvoice.RevisionNumber + 1;
            var unitPrice = ctx.Commercial.PricePerPole;
            var total = SurveyInvoiceRevision.CalculateTotal(newBillableCount, unitPrice, pricing.Value);
            if (total.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(total.Error);
            var newTotalAmount = total.Value;
            var newDueAmount = Math.Max(0m, newTotalAmount - ctx.Funding.ValidAllocatedDepositNet);
            var newOverpaymentAmount = Math.Max(0m, ctx.Funding.ValidAllocatedDepositNet - newTotalAmount);

            var newInvoiceFp = SurveyInvoiceRevision.ComputeEvidenceFingerprint(
                tenantId: order.TenantId,
                surveyOrderId: order.Id,
                farmId: order.FarmId,
                revisionNumber: newInvoiceRevisionNumber,
                approvedResultRevisionId: newApproved.Id,
                poleCountSnapshotId: newSnapshot.Id,
                verifiedBillablePoleCount: newBillableCount,
                surveyServicePriceId: order.SurveyServicePriceId!.Value,
                pricePerPole: unitPrice,
                currency: ctx.Commercial.Currency,
                commercialPolicyVersionId: order.BusinessPolicyVersionId!.Value,
                totalAmount: newTotalAmount,
                validAllocatedDepositNet: ctx.Funding.ValidAllocatedDepositNet,
                dueAmount: newDueAmount,
                overpaymentAmount: newOverpaymentAmount,
                finalizedBy: actorId,
                finalizedAt: correctedAt,
                finalizationReason: reason,
                predecessorRevisionId: currentInvoice.Id,
                zeroPoleApprovedBy: zeroPoleApprovedBy,
                zeroPoleReasonCode: normalizedZeroPoleReason,
                zeroPoleEvidenceReference: normalizedZeroPoleEvidence,
                comboPricing: pricing.Value);

            var newInvoiceResult = SurveyInvoiceRevision.Create(
                id: newInvoiceId,
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                revisionNumber: newInvoiceRevisionNumber,
                approvedResultRevisionId: newApproved.Id,
                poleCountSnapshotId: newSnapshot.Id,
                verifiedBillablePoleCount: newBillableCount,
                surveyServicePriceId: order.SurveyServicePriceId.Value,
                pricePerPole: unitPrice,
                currency: ctx.Commercial.Currency,
                commercialPolicyVersionId: order.BusinessPolicyVersionId.Value,
                validAllocatedDepositNet: ctx.Funding.ValidAllocatedDepositNet,
                finalizedBy: actorId,
                finalizedAt: correctedAt,
                finalizationReason: reason,
                evidenceFingerprint: newInvoiceFp,
                predecessorRevisionId: currentInvoice.Id,
                zeroPoleApprovedBy: zeroPoleApprovedBy,
                zeroPoleReasonCode: normalizedZeroPoleReason,
                zeroPoleEvidenceReference: normalizedZeroPoleEvidence,
                comboPricing: pricing.Value);

            if (newInvoiceResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(newInvoiceResult.Error);

            var newInvoice = newInvoiceResult.Value;

            // Step 1 (R5/C4): Supersede old revisions and flush first inside the same physical transaction
            var supersedeApprovedResult = ctx.ApprovedResult.Supersede(actorId, correctedAt, reason, newApproved.Id);
            if (supersedeApprovedResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(supersedeApprovedResult.Error);

            var supersedeInvoiceResult = currentInvoice.Supersede(actorId, correctedAt, reason, newInvoice.Id);
            if (supersedeInvoiceResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(supersedeInvoiceResult.Error);

            if (replacementReview is not null)
            {
                var supersedeReview = ctx.ComboEvidence!.Review.Supersede(actorId, correctedAt, reason, replacementReview.Id);
                if (supersedeReview.IsFailure)
                    return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(supersedeReview.Error);
            }

            await db.SaveChangesAsync(cancellationToken);

            // Step 2: Insert new snapshot, approved result, and invoice revision so FK principal keys exist
            if (replacementReview is not null)
                db.Set<SurveyComboReviewRevision>().Add(replacementReview);
            db.Set<SurveyOrderPoleCountSnapshotItem>().AddRange(newItems);
            db.Set<SurveyOrderPoleCountSnapshot>().Add(newSnapshot);
            db.Set<SurveyInternalApprovedResultRevision>().Add(newApproved);
            db.Set<SurveyInvoiceRevision>().Add(newInvoice);
            await db.SaveChangesAsync(cancellationToken);

            // Step 3: Invalidate open FinalBalance checkout obligations while keeping provider/invoice linkage for late receipts
            var invalidatedPaymentIds = new List<Guid>(ctx.OpenFinalBalanceCheckouts.Count);
            foreach (var checkout in ctx.OpenFinalBalanceCheckouts)
            {
                var invResult = checkout.InvalidateForInvoiceCorrection(actorId, correctedAt, reason, newInvoice.Id);
                if (invResult.IsFailure)
                    return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(invResult.Error);
                invalidatedPaymentIds.Add(checkout.Id);
            }

            // Step 4: Atomically advance both Order pointers in paired primitives (C4)
            var recordResult = order.RecordInternalApprovedResult(
                newApproved,
                ctx.ApprovedResult,
                newSnapshot,
                command.ExpectedOrderVersion);
            if (recordResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(recordResult.Error);

            var finalizeResult = order.FinalizeInvoiceRevision(
                newInvoice,
                currentInvoice,
                order.Version);
            if (finalizeResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(finalizeResult.Error);

            await db.SaveChangesAsync(cancellationToken);
            var response = new CorrectSurveyOrderPreReleaseInvoiceResponse(
                OrderId: order.Id,
                OrderVersion: order.Version,
                OrderStatus: order.Status.ToString(),
                ServiceCode: newApproved.ServiceCode,
                ScreensDisease: newApproved.ScreensDisease,
                PreviousCountSnapshotId: ctx.CountSnapshot.Id,
                NewCountSnapshotId: newSnapshot.Id,
                NewCountSnapshotRevisionNumber: newSnapshot.RevisionNumber,
                NewCountSnapshotFingerprint: newSnapshot.EvidenceFingerprint,
                PreviousVerifiedBillablePoleCount: ctx.CountSnapshot.VerifiedBillablePoleCount,
                NewVerifiedBillablePoleCount: newInvoice.VerifiedBillablePoleCount,
                PreviousApprovedResultRevisionId: ctx.ApprovedResult.Id,
                NewApprovedResultRevisionId: newApproved.Id,
                NewApprovedResultRevisionNumber: newApproved.RevisionNumber,
                NewApprovedResultFingerprint: newApproved.EvidenceFingerprint,
                PreviousInvoiceRevisionId: currentInvoice.Id,
                NewInvoiceRevisionId: newInvoice.Id,
                NewInvoiceRevisionNumber: newInvoice.RevisionNumber,
                NewInvoiceFingerprint: newInvoice.EvidenceFingerprint,
                SurveyServicePriceId: newInvoice.SurveyServicePriceId,
                PricePerPole: newInvoice.PricePerPole,
                Currency: newInvoice.Currency,
                CommercialPolicyVersionId: newInvoice.CommercialPolicyVersionId,
                TotalAmount: newInvoice.TotalAmount,
                ValidAllocatedDepositNet: newInvoice.ValidAllocatedDepositNet,
                ConfirmedNetReceipts: ctx.Funding.ConfirmedNetReceipts,
                DueAmount: newInvoice.DueAmount,
                OverpaymentAmount: newInvoice.OverpaymentAmount,
                InvalidatedCheckoutPaymentIds: invalidatedPaymentIds,
                ZeroPoleApprovedBy: newInvoice.ZeroPoleApprovedBy,
                ZeroPoleReasonCode: newInvoice.ZeroPoleReasonCode,
                ZeroPoleEvidenceReference: newInvoice.ZeroPoleEvidenceReference,
                CorrectedAt: correctedAt,
                IsReplay: false,
                ComboReviewRevisionId: effectiveReview?.Id,
                ComboReviewFingerprint: effectiveReview?.DecisionFingerprint);

            var correlationId = ResolveCorrelationId();
            var outcomeDoc = JsonSerializer.SerializeToDocument(response, CanonicalJsonOptions);
            var opResult = SurveyResultReviewOperation.Create(
                tenantId: order.TenantId,
                farmId: order.FarmId,
                surveyOrderId: order.Id,
                actorId: actorId,
                action: SurveyResultReviewOperation.ActionCorrectPreReleaseInvoice,
                apiVersion: SurveyResultReviewOperation.ApiVersionV3,
                operationId: command.OperationId,
                requestFingerprint: requestFingerprint,
                outcome: outcomeDoc,
                createdAt: correctedAt,
                comboBatchId: newApproved.ComboBatchId,
                reviewRevisionId: newApproved.ComboReviewRevisionId,
                approvedResultRevisionId: newApproved.Id,
                captureAttemptId: ctx.Attempt.Id,
                poleCountSnapshotId: newSnapshot.Id,
                invoiceRevisionId: newInvoice.Id);

            if (opResult.IsFailure)
                return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(opResult.Error);

            db.Set<SurveyResultReviewOperation>().Add(opResult.Value);

            using var oldAuditDoc = JsonSerializer.SerializeToDocument(new
            {
                CountSnapshotId = ctx.CountSnapshot.Id,
                ctx.CountSnapshot.VerifiedBillablePoleCount,
                ApprovedResultRevisionId = ctx.ApprovedResult.Id,
                InvoiceRevisionId = currentInvoice.Id,
                currentInvoice.TotalAmount,
                currentInvoice.DueAmount
            });
            using var newAuditDoc = JsonSerializer.SerializeToDocument(new
            {
                CountSnapshotId = newSnapshot.Id,
                newSnapshot.VerifiedBillablePoleCount,
                ApprovedResultRevisionId = newApproved.Id,
                InvoiceRevisionId = newInvoice.Id,
                newInvoice.TotalAmount,
                newInvoice.ValidAllocatedDepositNet,
                ctx.Funding.ConfirmedNetReceipts,
                newInvoice.DueAmount,
                newInvoice.OverpaymentAmount,
                InvalidatedCheckoutPaymentIds = invalidatedPaymentIds,
                ComboReviewReverified = ctx.ComboEvidence is not null,
                VerifiedComboReviewRevisionId = effectiveReview?.Id
            });
            db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                order.TenantId,
                order.FarmId,
                actorId,
                correlationId,
                nameof(SurveyInvoiceRevision),
                newInvoice.Id,
                "PRE_RELEASE_INVOICE_CORRECTED",
                oldData: oldAuditDoc,
                newData: newAuditDoc,
                createdAt: correctedAt));

            AddStateChangedOutbox(order, actorId, correlationId, "PRE_RELEASE_INVOICE_CORRECTED", correctedAt, ctx.Attempt.Id, ctx.Mission.Id);

            AddReleaseEvaluationOutbox(
                order, newInvoice, newApproved, newSnapshot, SurveyReleaseTriggerSource.PreReleaseCorrection,
                command.OperationId, requestFingerprint, actorId, correlationId, correctedAt);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result.Success(response);
        }
        catch (Fault f)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(f.Error);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException or PostgresException or SurveyDomainException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<CorrectSurveyOrderPreReleaseInvoiceResponse>(MapPersistenceException("InvoiceCorrection", ex));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<Result<VerifiedInvoiceContext>> LockAndValidateOrderedContextAsync(
        SurveyOrder order,
        bool isCorrection,
        CancellationToken cancellationToken)
    {
        if (order.Scope != ContractScope.V3)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("SurveyOrder.ScopeInvalid", "Only V3 orders are supported by V3 invoice operations."));

        if (order.Status == SurveyOrderStatus.Cancelled || order.Status == SurveyOrderStatus.Completed)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("SurveyOrder.Terminal", $"Order in terminal status '{order.Status}' cannot be invoiced."));

        if (!order.CurrentApprovedResultRevisionId.HasValue || !order.VerifiedPoleCountSnapshotId.HasValue)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("ApprovedResult.Missing", "Order must have a frozen internal approved result and count snapshot before invoicing."));

        if (!order.BusinessPolicyVersionId.HasValue ||
            !order.SurveyServicePriceId.HasValue ||
            !order.PricePerPoleSnapshot.HasValue ||
            string.IsNullOrWhiteSpace(order.Currency))
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("SurveyOrder.CommercialTermsMissing", "Order is missing pinned commercial policy or price terms."));
        }

        var commercial = await db.Set<SurveyOrderCommercialSnapshot>().AsNoTracking()
            .SingleOrDefaultAsync(c => c.SurveyOrderId == order.Id, cancellationToken);
        if (commercial is null)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("CommercialSnapshot.Missing", "Pinned commercial snapshot was not found."));

        if (commercial.BusinessPolicyVersionId != order.BusinessPolicyVersionId.Value ||
            commercial.PriceVersionId != order.SurveyServicePriceId.Value ||
            commercial.PricePerPole != order.PricePerPoleSnapshot.Value.Amount ||
            !string.Equals(commercial.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("CommercialSnapshot.Mismatch", "Order commercial fields do not match pinned commercial snapshot."));
        }

        // =========================================================================
        // ADR 0020 RANK 5: Deposit obligation -> Checkout -> Funding/Cancellation/Refund facts
        // All Rank-5 locks and reads complete BEFORE any Rank-6 evidence locks.
        // =========================================================================
        SurveyDepositObligation? depositObligation = null;
        if (order.DepositObligationId.HasValue)
        {
            depositObligation = await db.Set<SurveyDepositObligation>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_deposit_obligations WHERE id = {order.DepositObligationId.Value} AND survey_order_id = {order.Id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (depositObligation is null || depositObligation.Status == SurveyDepositObligationStatus.Cancelled)
                return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("DepositObligation.Invalid", "Deposit obligation is missing or cancelled."));
        }

        var orderPayments = await db.Set<SurveyPayment>()
            .FromSqlInterpolated($"""
                SELECT payment.*, payment.xmin FROM survey.survey_payments AS payment
                WHERE payment.survey_order_id = {order.Id}
                ORDER BY payment.id
                FOR UPDATE
                """)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        List<SurveyPayment> openFinalBalanceCheckouts = isCorrection
            ? orderPayments.Values
                .Where(p => p.Purpose == PaymentPurpose.FinalBalance &&
                            p.Status is SurveyPaymentStatus.Pending or
                                SurveyPaymentStatus.Processing or
                                SurveyPaymentStatus.AdjustmentRequired)
                .OrderBy(p => p.Id)
                .ToList()
            : [];

        var fundingResult = await SurveyInvoiceFunding.ReadAsync(
            db,
            order,
            depositObligation,
            currentInvoiceRevisionId: isCorrection ? order.CurrentInvoiceRevisionId : null,
            lockRows: true,
            cancellationToken,
            preloadedOrderPayments: orderPayments);
        if (fundingResult.IsFailure)
            return Result.Failure<VerifiedInvoiceContext>(fundingResult.Error);

        if (fundingResult.Value.HasOpenReconciliation)
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict(
                SurveyInvoiceFunding.HoldErrorCode,
                string.Join(" ", fundingResult.Value.ReconciliationReasons)));
        }

        if (fundingResult.Value.ActiveOtherRefundReservations > 0m)
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict(
                SurveyInvoiceFunding.HoldErrorCode,
                "Order has active refund reservations for another origin and requires reconciliation before invoicing."));
        }

        var hasCancellation = await db.Set<SurveyCancellationDecision>()
            .FromSqlInterpolated($"SELECT * FROM survey.survey_cancellation_decisions WHERE survey_order_id = {order.Id} FOR SHARE")
            .AnyAsync(cancellationToken);
        if (hasCancellation)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("SurveyOrder.Cancelled", "Order has a recorded cancellation decision."));

        // =========================================================================
        // ADR 0020 RANK 6: Invoice / ApprovedResult / CountSnapshot / MappingBatch / Map / Boundary / Exceptions
        // =========================================================================
        SurveyInvoiceRevision? currentInvoice = null;
        if (isCorrection)
        {
            currentInvoice = await db.Set<SurveyInvoiceRevision>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_invoice_revisions WHERE id = {order.CurrentInvoiceRevisionId!.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (currentInvoice is null || currentInvoice.Status != SurveyInvoiceRevision.StatusCurrent)
                return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("InvoiceRevision.ConsistencyConflict", "Current invoice revision is missing or not in CURRENT status."));
        }

        var approvedSql = isCorrection
            ? db.Set<SurveyInternalApprovedResultRevision>().FromSqlInterpolated(
                $"SELECT * FROM survey.survey_internal_approved_result_revisions WHERE id = {order.CurrentApprovedResultRevisionId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR UPDATE")
            : db.Set<SurveyInternalApprovedResultRevision>().FromSqlInterpolated(
                $"SELECT * FROM survey.survey_internal_approved_result_revisions WHERE id = {order.CurrentApprovedResultRevisionId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE");

        var approved = await approvedSql.SingleOrDefaultAsync(cancellationToken);
        if (approved is null || !approved.IsCurrent)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("ApprovedResult.Invalid", "Current internal approved result revision is missing or not active."));

        if (isCorrection &&
            (currentInvoice!.ApprovedResultRevisionId != approved.Id ||
             currentInvoice.PoleCountSnapshotId != order.VerifiedPoleCountSnapshotId.Value))
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("InvoiceRevision.ConsistencyConflict", "Current invoice revision does not match current approval and snapshot pointers."));
        }

        if (approved.ServiceCode != commercial.ServiceCode || approved.ScreensDisease != commercial.ScreensDisease)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("ApprovedResult.ServiceMismatch", "Approved result service capability does not match pinned commercial snapshot."));

        var recomputedApprovalFp = SurveyInternalApprovedResultRevision.ComputeEvidenceFingerprint(
            approved.TenantId, approved.SurveyOrderId, approved.FarmId, approved.RevisionNumber,
            approved.ServiceCode, approved.ScreensDisease, approved.CaptureAttemptId, approved.MissionId,
            approved.FarmBaseMapVersionId, approved.BaselineMapVersionId, approved.FarmBoundaryVersionId,
            approved.PoleCountSnapshotId, approved.MappingCandidateBatchId, approved.ComboBatchId,
            approved.ReviewDecision, approved.HealthAssessmentStatus, approved.LimitationNotice,
            approved.ReviewedBy, approved.ReviewedAt, approved.ReviewReason, approved.PredecessorRevisionId,
            approved.ComboReviewRevisionId, approved.ComboReviewFingerprint);

        if (!string.Equals(recomputedApprovalFp, approved.EvidenceFingerprint, StringComparison.Ordinal))
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("ApprovedResult.FingerprintMismatch", "Stored approved result revision failed fingerprint integrity check."));

        var snapshot = await db.Set<SurveyOrderPoleCountSnapshot>()
            .FromSqlInterpolated($"SELECT * FROM survey.survey_order_pole_count_snapshots WHERE id = {order.VerifiedPoleCountSnapshotId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
            .Include(s => s.Items)
            .SingleOrDefaultAsync(cancellationToken);

        if (snapshot is null || snapshot.Id != approved.PoleCountSnapshotId)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("CountSnapshot.Mismatch", "Order verified pole count snapshot does not match approved result snapshot."));

        if (snapshot.MissionId != approved.MissionId ||
            snapshot.CaptureAttemptId != approved.CaptureAttemptId ||
            snapshot.FarmBaseMapVersionId != approved.FarmBaseMapVersionId ||
            snapshot.FarmBoundaryVersionId != approved.FarmBoundaryVersionId ||
            snapshot.MappingCandidateBatchId != approved.MappingCandidateBatchId)
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("CountSnapshot.ProvenanceMismatch", "Pole count snapshot provenance does not match approved result revision."));
        }

        var distinctVerifiedCount = snapshot.Items
            .Where(x => x.IsAccepted && x.IsStrictlyInBoundary && x.PlantProfileId.HasValue)
            .Select(x => x.PlantProfileId!.Value)
            .Distinct()
            .Count();

        if (distinctVerifiedCount != snapshot.VerifiedBillablePoleCount ||
            order.VerifiedBillablePoleCount != snapshot.VerifiedBillablePoleCount)
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("CountSnapshot.CountIntegrityMismatch", "Stored verified billable pole count does not match distinct verified in-boundary profiles."));
        }

        IQueryable<MappingCandidateBatch> batchQuery = db.Set<MappingCandidateBatch>()
            .FromSqlInterpolated($"SELECT *, xmin FROM farm.mapping_candidate_batches WHERE id = {snapshot.MappingCandidateBatchId} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE");
        if (isCorrection)
            batchQuery = batchQuery.Include(b => b.Candidates);

        var mappingBatch = await batchQuery.SingleOrDefaultAsync(cancellationToken);
        var baseMap = await db.Set<FarmBaseMapVersion>()
            .FromSqlInterpolated($"SELECT *, xmin FROM farm.farm_base_map_versions WHERE id = {snapshot.FarmBaseMapVersionId} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
            .SingleOrDefaultAsync(cancellationToken);
        var boundary = await db.Set<FarmBoundary>()
            .FromSqlInterpolated($"SELECT *, xmin FROM farm.farm_boundaries WHERE id = {snapshot.FarmBoundaryVersionId} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
            .SingleOrDefaultAsync(cancellationToken);

        if (mappingBatch is null || baseMap is null || boundary is null)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("BaselineProof.Missing", "Mapping batch, base map, or boundary proof could not be found."));

        if (mappingBatch.Status is not (MappingCandidateBatchStatus.Approved or MappingCandidateBatchStatus.Applied))
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("MappingBatch.NotApproved", "Mapping candidate batch is not in an approved/applied state."));

        if (boundary.ReviewedAt is null || boundary.Status is not (FarmBoundaryStatus.Approved or FarmBoundaryStatus.Superseded))
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("Boundary.NotApproved", "Capture farm boundary is not in a valid approved state."));

        if (baseMap.Status is not (FarmBaseMapStatus.InternalApproved or FarmBaseMapStatus.Published) ||
            baseMap.FarmBoundaryVersionId != boundary.Id)
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("BaseMap.Invalid", "Capture base map must be internally approved or published for the exact boundary."));
        }

        if (isCorrection && baseMap.Status == FarmBaseMapStatus.Published)
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("InvoiceCorrection.AlreadyReleased", "Pre-release correction cannot proceed after the capture base map has been published."));
        }

        Guid baselineMapVersionId;
        if (order.RequiresBaselineMapping)
        {
            if (mappingBatch.MappingKind != MappingKind.FirstBaseline ||
                !baseMap.IsBaseline ||
                baseMap.BaselineMapVersionId != baseMap.Id ||
                approved.BaselineMapVersionId != baseMap.Id)
            {
                return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("BaselineProof.InvalidFirstBaseline", "First-baseline order requires a self-referencing baseline map."));
            }
            baselineMapVersionId = baseMap.Id;
        }
        else
        {
            if (mappingBatch.MappingKind != MappingKind.FollowUpSnapshot ||
                baseMap.IsBaseline ||
                !order.BaselineMapVersionId.HasValue ||
                baseMap.BaselineMapVersionId != order.BaselineMapVersionId.Value ||
                approved.BaselineMapVersionId != order.BaselineMapVersionId.Value)
            {
                return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("BaselineProof.InvalidFollowUp", "Follow-up order must keep its pinned baseline map version."));
            }
            baselineMapVersionId = order.BaselineMapVersionId.Value;
        }

        var recomputedSnapshotFp = SurveyOrderPoleCountSnapshot.ComputeEvidenceFingerprint(
            snapshot.TenantId, snapshot.SurveyOrderId, snapshot.FarmId, snapshot.MissionId,
            snapshot.CaptureAttemptId, snapshot.FarmBaseMapVersionId, snapshot.FarmBoundaryVersionId,
            snapshot.MappingCandidateBatchId, mappingBatch.SpatialPolicyFingerprint, snapshot.Items);

        if (!string.Equals(recomputedSnapshotFp, snapshot.EvidenceFingerprint, StringComparison.Ordinal))
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("CountSnapshot.FingerprintMismatch", "Verified pole count snapshot failed fingerprint integrity check."));

        var unresolvedBoundaryExceptions = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value"
            FROM farm.boundary_exceptions
            WHERE tenant_id = {order.TenantId}
              AND farm_id = {order.FarmId}
              AND farm_boundary_version_id = {snapshot.FarmBoundaryVersionId}
              AND (survey_order_id = {order.Id} OR mission_id = {snapshot.MissionId})
              AND state <> 'RESOLVED'::system.boundary_exception_state
            LIMIT 1
            FOR SHARE
            """).ToListAsync(cancellationToken);

        if (unresolvedBoundaryExceptions.Count > 0)
        {
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict(
                "BoundaryException.Unresolved",
                "Unresolved boundary exceptions exist for this survey order/mission and boundary version. Resolve all exceptions before finalizing invoice."));
        }

        ComboEvidenceVerification? comboEvidence = null;
        if (approved.ServiceCode == SurveyInternalApprovedResultRevision.ServiceCombo && approved.ScreensDisease)
        {
            if (!approved.ComboBatchId.HasValue || !approved.ComboReviewRevisionId.HasValue || string.IsNullOrWhiteSpace(approved.ComboReviewFingerprint))
                return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("ComboReview.Missing", "COMBO approved result lacks COMBO batch or review linkage."));

            var comboBatch = await db.Set<SurveyComboImportBatch>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_combo_import_batches WHERE id = {approved.ComboBatchId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            if (comboBatch is null ||
                comboBatch.MappingCandidateBatchId != snapshot.MappingCandidateBatchId ||
                comboBatch.FarmBaseMapVersionId != snapshot.FarmBaseMapVersionId ||
                comboBatch.FarmBoundaryVersionId != snapshot.FarmBoundaryVersionId ||
                comboBatch.BaselineMapVersionId != baselineMapVersionId)
                return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("ComboBatch.Invalid", "Referenced COMBO batch is missing or inconsistent."));

            var reviewQuery = isCorrection
                ? db.Set<SurveyComboReviewRevision>().FromSqlInterpolated(
                    $"SELECT * FROM survey.survey_combo_review_revisions WHERE id = {approved.ComboReviewRevisionId.Value} AND combo_batch_id = {comboBatch.Id} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR UPDATE")
                : db.Set<SurveyComboReviewRevision>().FromSqlInterpolated(
                    $"SELECT * FROM survey.survey_combo_review_revisions WHERE id = {approved.ComboReviewRevisionId.Value} AND combo_batch_id = {comboBatch.Id} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE");
            var comboReview = await reviewQuery.SingleOrDefaultAsync(cancellationToken);

            if (comboReview is null ||
                !comboReview.IsCurrent ||
                comboReview.Outcome != SurveyComboReviewRevision.OutcomeApproved ||
                comboReview.UnresolvedBlockersCount > 0 ||
                !string.Equals(comboReview.DecisionFingerprint, approved.ComboReviewFingerprint, StringComparison.Ordinal))
            {
                return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("ComboReview.NotApproved", "Referenced COMBO review is stale, not Approved, or still has unresolved blockers."));
            }

            ComboAnalysisReadyV1? comboPayload;
            try
            {
                comboPayload = comboBatch.RawPayload.Deserialize<ComboAnalysisReadyV1>(PayloadJsonOptions);
            }
            catch (JsonException)
            {
                comboPayload = null;
            }

            var decisionsResult = comboReview.ReadDecisions();
            if (comboPayload is null || decisionsResult.IsFailure)
                return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("ComboReview.CorruptedPayload", "Persisted COMBO batch or review decisions cannot be deserialized."));

            comboEvidence = new ComboEvidenceVerification(comboBatch, comboPayload, comboReview, decisionsResult.Value);
        }

        // =========================================================================
        // ADR 0020 RANK 7 & RANK 8: CaptureAttempt -> DroneMission
        // =========================================================================
        var attempt = await db.Set<CaptureAttempt>()
            .FromSqlInterpolated($"SELECT *, xmin FROM survey.capture_attempts WHERE id = {approved.CaptureAttemptId} AND order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
            .SingleOrDefaultAsync(cancellationToken);

        if (attempt is null || attempt.Status != CaptureAttemptStatus.Accepted)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("CaptureAttempt.NotAccepted", "Associated capture attempt must be in Accepted status."));

        if (mappingBatch.MissionId != approved.MissionId || mappingBatch.CaptureAttemptId != attempt.Id)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("Mission.AttemptMismatch", "Attempt, approval, and mapping batch disagree on execution Mission."));

        var mission = await db.Set<DroneMission>()
            .FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {approved.MissionId} AND farm_id = {order.FarmId} FOR SHARE")
            .SingleOrDefaultAsync(cancellationToken);

        if (mission is null || mission.TenantId != order.TenantId || mission.SurveyOrderId != order.Id || mission.CaptureAttemptId != attempt.Id)
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict("Mission.ContextMismatch", "Execution Mission is missing or no longer bound to this order/attempt."));

        if (comboEvidence is not null && !await DiseaseCaptureMappingProof.HasAcceptedDiseaseCaptureAsync(
                db, order, snapshot, approved, comboEvidence.Batch, cancellationToken))
            return Result.Failure<VerifiedInvoiceContext>(AppError.Conflict(
                "ComboBatch.CaptureMismatch", "COMBO disease evidence does not come from the accepted capture for this map."));

        return Result.Success(new VerifiedInvoiceContext(
            order,
            commercial,
            fundingResult.Value,
            openFinalBalanceCheckouts,
            currentInvoice,
            approved,
            snapshot,
            mappingBatch,
            baseMap,
            boundary,
            attempt,
            mission,
            comboEvidence,
            baselineMapVersionId));
    }

    // R05E-04: Corrected snapshot items must resolve PlantProfileId strictly from mapping-review provenance
    private async Task<Result<List<SurveyOrderPoleCountSnapshotItem>>> BuildCorrectedSnapshotItemsAsync(
        SurveyOrder order,
        SurveyOrderPoleCountSnapshot currentSnapshot,
        MappingCandidateBatch mappingBatch,
        FarmBoundary boundary,
        Guid newSnapshotId,
        IReadOnlyList<PreReleasePoleCountItemCorrection> orderedCorrections,
        CancellationToken cancellationToken)
    {
        var existingByCandidateId = currentSnapshot.Items.ToDictionary(i => i.MappingCandidateId);
        var correctionsByCandidateId = orderedCorrections.ToDictionary(c => c.MappingCandidateId);

        foreach (var candidateId in correctionsByCandidateId.Keys)
        {
            if (!existingByCandidateId.ContainsKey(candidateId))
            {
                return Result.Failure<List<SurveyOrderPoleCountSnapshotItem>>(AppError.Validation(
                    "ItemCorrections.UnknownCandidate",
                    $"MappingCandidate '{candidateId}' does not belong to current pole count snapshot '{currentSnapshot.Id}'."));
            }
        }

        var candidateLookup = mappingBatch.Candidates.ToDictionary(c => c.Id);

        // Load mapping-review provenance observations for this batch under FOR SHARE
        var candidateIds = currentSnapshot.Items.Select(i => i.MappingCandidateId).Distinct().ToArray();
        var obsLookup = new Dictionary<Guid, Guid>();
        if (candidateIds.Length > 0)
        {
            await using var obsCmd = CreateCommand("""
                SELECT candidate_id, plant_profile_id
                FROM plant.plant_profile_observations
                WHERE mapping_candidate_batch_id = @batch_id
                  AND tenant_id = @tenant_id
                  AND farm_id = @farm_id
                  AND candidate_id = ANY(@candidate_ids)
                FOR SHARE
                """);
            obsCmd.Parameters.Add(new NpgsqlParameter("batch_id", mappingBatch.Id));
            obsCmd.Parameters.Add(new NpgsqlParameter("tenant_id", order.TenantId));
            obsCmd.Parameters.Add(new NpgsqlParameter("farm_id", order.FarmId));
            obsCmd.Parameters.Add(new NpgsqlParameter("candidate_ids", candidateIds));
            await using var reader = await obsCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var cid = reader.GetGuid(0);
                var pid = reader.GetGuid(1);
                obsLookup.TryAdd(cid, pid);
            }
        }

        // Load plants referenced by matched candidates (MatchExisting / Relocate / Replace) under FOR SHARE
        var matchedPlantIds = candidateLookup.Values
            .Select(c => c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var plantProfileByPlantId = new Dictionary<Guid, Guid>();
        if (matchedPlantIds.Length > 0)
        {
            await using var plantCmd = CreateCommand("""
                SELECT id, plant_profile_id
                FROM plant.plants
                WHERE id = ANY(@plant_ids)
                  AND tenant_id = @tenant_id
                  AND farm_id = @farm_id
                  AND plant_profile_id IS NOT NULL
                FOR SHARE
                """);
            plantCmd.Parameters.Add(new NpgsqlParameter("plant_ids", matchedPlantIds));
            plantCmd.Parameters.Add(new NpgsqlParameter("tenant_id", order.TenantId));
            plantCmd.Parameters.Add(new NpgsqlParameter("farm_id", order.FarmId));
            await using var reader = await plantCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                plantProfileByPlantId[reader.GetGuid(0)] = reader.GetGuid(1);
            }
        }

        var newItems = new List<SurveyOrderPoleCountSnapshotItem>(currentSnapshot.Items.Count);
        foreach (var existing in currentSnapshot.Items.OrderBy(i => i.SourceCandidateId))
        {
            if (!candidateLookup.TryGetValue(existing.MappingCandidateId, out var candidate))
            {
                return Result.Failure<List<SurveyOrderPoleCountSnapshotItem>>(AppError.Conflict(
                    "MappingCandidate.Missing",
                    $"MappingCandidate '{existing.MappingCandidateId}' referenced by snapshot was not found in batch."));
            }

            bool isAccepted;
            bool isStrictlyInBoundary;
            Guid? plantProfileId;
            string? exclusionReason;

            if (correctionsByCandidateId.TryGetValue(existing.MappingCandidateId, out var correction))
            {
                isStrictlyInBoundary = await IsPointStrictlyInsideBoundaryAsync(
                    order.TenantId,
                    order.FarmId,
                    boundary.Id,
                    existing.EffectivePosition,
                    cancellationToken);

                if (correction.IsAccepted)
                {
                    if (candidate.ReviewDecision is MappingCandidateReviewDecision.Pending or MappingCandidateReviewDecision.Reject)
                    {
                        return Result.Failure<List<SurveyOrderPoleCountSnapshotItem>>(AppError.Conflict(
                            "MappingCandidate.InvalidReviewDecision",
                            $"MappingCandidate '{candidate.Id}' is not in an approved mapping decision state."));
                    }

                    if (!isStrictlyInBoundary)
                    {
                        return Result.Failure<List<SurveyOrderPoleCountSnapshotItem>>(AppError.Validation(
                            "InvoiceCorrection.OutOfBoundaryCannotAccept",
                            $"MappingCandidate '{existing.MappingCandidateId}' is not strictly inside approved farm boundary and cannot be accepted."));
                    }

                    Guid? provenanceProfileId = existing.PlantProfileId;
                    if (!provenanceProfileId.HasValue && obsLookup.TryGetValue(existing.MappingCandidateId, out var obsProfileId))
                    {
                        provenanceProfileId = obsProfileId;
                    }
                    else if (!provenanceProfileId.HasValue &&
                             existing.ReviewDecision == "MatchExisting" &&
                             existing.TargetPlantId.HasValue &&
                             plantProfileByPlantId.TryGetValue(existing.TargetPlantId.Value, out var matchedProfileId))
                    {
                        provenanceProfileId = matchedProfileId;
                    }

                    if (!provenanceProfileId.HasValue || provenanceProfileId.Value == Guid.Empty)
                    {
                        return Result.Failure<List<SurveyOrderPoleCountSnapshotItem>>(AppError.Conflict(
                            "InvoiceCorrection.MappingReviewRequired",
                            $"MappingCandidate '{existing.MappingCandidateId}' lacks reviewed mapping-identity provenance for acceptance. Update plant identity via mapping review first."));
                    }

                    if (correction.ExpectedPlantProfileId.HasValue &&
                        correction.ExpectedPlantProfileId.Value != provenanceProfileId.Value)
                    {
                        return Result.Failure<List<SurveyOrderPoleCountSnapshotItem>>(AppError.Validation(
                            "InvoiceCorrection.ProfileProvenanceMismatch",
                            $"ExpectedPlantProfileId '{correction.ExpectedPlantProfileId.Value}' does not match reviewed mapping provenance '{provenanceProfileId.Value}'."));
                    }

                    isAccepted = true;
                    plantProfileId = provenanceProfileId.Value;
                    exclusionReason = null;
                }
                else
                {
                    isAccepted = false;
                    plantProfileId = null;
                    exclusionReason = correction.ExclusionReason!.Trim();
                }
            }
            else
            {
                isAccepted = existing.IsAccepted;
                isStrictlyInBoundary = existing.IsStrictlyInBoundary;
                plantProfileId = existing.PlantProfileId;
                exclusionReason = existing.ExclusionReason;
            }

            newItems.Add(SurveyOrderPoleCountSnapshotItem.Create(
                id: Guid.CreateVersion7(),
                snapshotId: newSnapshotId,
                tenantId: order.TenantId,
                farmId: order.FarmId,
                mappingCandidateBatchId: mappingBatch.Id,
                mappingCandidateId: existing.MappingCandidateId,
                sourceCandidateId: existing.SourceCandidateId,
                plantProfileId: plantProfileId,
                effectivePosition: (Point)existing.EffectivePosition.Copy(),
                reviewDecision: existing.ReviewDecision,
                isAccepted: isAccepted,
                isStrictlyInBoundary: isStrictlyInBoundary,
                exclusionReason: exclusionReason,
                targetPlantId: existing.TargetPlantId,
                targetPlantProfileId: existing.TargetPlantProfileId,
                previousPosition: existing.PreviousPosition is null ? null : (Point)existing.PreviousPosition.Copy(),
                effectiveRowIndex: existing.EffectiveRowIndex,
                effectiveColumnIndex: existing.EffectiveColumnIndex));
        }

        // Verify all resultant accepted PlantProfileIds exist in plant.plant_profiles for this tenant/farm (FOR SHARE)
        var acceptedProfileIds = newItems
            .Where(x => x.IsAccepted && x.PlantProfileId.HasValue)
            .Select(x => x.PlantProfileId!.Value)
            .Distinct()
            .ToArray();

        if (acceptedProfileIds.Length > 0)
        {
            var foundProfiles = await db.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value"
                FROM plant.plant_profiles
                WHERE id = ANY({acceptedProfileIds})
                  AND tenant_id = {order.TenantId}
                  AND farm_id = {order.FarmId}
                FOR SHARE
                """).ToListAsync(cancellationToken);

            if (foundProfiles.ToHashSet().Count != acceptedProfileIds.Length)
            {
                return Result.Failure<List<SurveyOrderPoleCountSnapshotItem>>(AppError.Conflict(
                    "InvoiceCorrection.PlantProfileNotFound",
                    "One or more accepted plant profiles do not belong to this tenant/farm."));
            }
        }

        return Result.Success(newItems);
    }

    // R05E-04: Ensure accepted COMBO findings & approved zone memberships remain bound to accepted poles in the corrected snapshot
    private async Task<Result<SurveyComboReviewRevision>> CreateReplacementComboReviewAsync(
        SurveyOrder order,
        ComboEvidenceVerification combo,
        PreReleaseComboReview replacement,
        Guid actorId,
        string reason,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken)
    {
        var previous = combo.Review;
        if (previous.RevisionNumber == int.MaxValue)
            return Result.Failure<SurveyComboReviewRevision>(AppError.Conflict("Revision.LimitReached", "COMBO review sequence reached its maximum."));

        var validation = await resultReviewTransaction.ValidateCorrectionComboReviewAsync(
            order, combo.Batch, combo.Payload, replacement.Decisions, replacement.ConfirmNoDiseaseObserved,
            replacement.NegativeResultConfirmationEvidence, reviewedAt, cancellationToken);
        if (validation.IsFailure)
            return Result.Failure<SurveyComboReviewRevision>(validation.Error);

        const string outcome = SurveyComboReviewRevision.OutcomeApproved;
        var revisionNumber = previous.RevisionNumber + 1;
        var fingerprint = SurveyComboReviewRevision.ComputeDecisionFingerprint(
            order.TenantId, order.FarmId, order.Id, previous.CaptureAttemptId, combo.Batch.Id,
            combo.Batch.SourceRevision, combo.Batch.CanonicalFingerprint, revisionNumber, previous.Id,
            outcome, reason, replacement.ConfirmNoDiseaseObserved, replacement.NegativeResultConfirmationEvidence,
            actorId, reviewedAt, replacement.Decisions);

        return SurveyComboReviewRevision.Create(
            Guid.CreateVersion7(), order.TenantId, order.FarmId, order.Id, previous.CaptureAttemptId, combo.Batch.Id,
            combo.Batch.SourceRevision, combo.Batch.CanonicalFingerprint, revisionNumber, previous.Id, outcome, reason,
            replacement.ConfirmNoDiseaseObserved, replacement.NegativeResultConfirmationEvidence, actorId,
            reviewedAt, replacement.Decisions, fingerprint);
    }

    private static Result VerifyComboReviewAgainstCorrectedItems(
        ComboAnalysisReadyV1 payload,
        ComboReviewDecisionsSnapshotRecord decisions,
        IReadOnlyList<SurveyOrderPoleCountSnapshotItem> correctedItems)
    {
        var acceptedPairs = correctedItems
            .Where(i => i.IsAccepted && i.IsStrictlyInBoundary && i.PlantProfileId.HasValue)
            .SelectMany(i => new[]
            {
                (i.MappingCandidateId, i.PlantProfileId!.Value),
                (i.SourceCandidateId, i.PlantProfileId!.Value)
            })
            .ToHashSet();

        var acceptedProfiles = acceptedPairs.Select(pair => pair.Value).ToHashSet();
        var findingsById = payload.Findings.ToDictionary(f => f.FindingId);
        var acceptedFindingIds = new HashSet<Guid>();

        foreach (var fd in decisions.FindingDecisions)
        {
            if (fd.Decision != SurveyComboReviewRevision.FindingDecisionAccept)
                continue;

            if (!findingsById.TryGetValue(fd.FindingId, out var finding))
            {
                return Result.Failure(AppError.Conflict(
                    "InvoiceCorrection.ComboReviewInvalidatedByCountChange",
                    $"Accepted COMBO finding '{fd.FindingId}' was not found in batch payload."));
            }

            var effectiveProfileId = fd.ReviewedPlantProfileId ?? finding.PlantProfileId;
            var matchesCorrectedCount = effectiveProfileId is Guid profileId &&
                (!fd.ReviewedPlantProfileId.HasValue && finding.MappingCandidateId is Guid candidateId
                    ? acceptedPairs.Contains((candidateId, profileId))
                    : acceptedProfiles.Contains(profileId));
            if (!matchesCorrectedCount)
            {
                return Result.Failure(AppError.Conflict(
                    "InvoiceCorrection.ComboReviewInvalidatedByCountChange",
                    $"Corrected pole set excludes candidate/profile ({finding.MappingCandidateId}, {finding.PlantProfileId}) referenced by accepted COMBO finding '{fd.FindingId}'. Supply replacement COMBO review decisions with the correction."));
            }

            acceptedFindingIds.Add(fd.FindingId);
        }

        var zonesById = payload.DiseaseZones.ToDictionary(z => z.SourceProposalId.Trim(), StringComparer.Ordinal);
        foreach (var zd in decisions.ZoneDecisions)
        {
            if (zd.Decision == SurveyComboReviewRevision.ZoneDecisionReject)
                continue;

            if (!zonesById.TryGetValue(zd.SourceProposalId.Trim(), out var proposal))
            {
                return Result.Failure(AppError.Conflict(
                    "InvoiceCorrection.ComboReviewInvalidatedByCountChange",
                    $"Approved COMBO disease zone '{zd.SourceProposalId}' was not found in batch payload."));
            }

            var memberFindingIds = zd.ReviewedFindingIds ?? proposal.Memberships.Select(m => m.FindingId).ToArray();
            foreach (var memberFindingId in memberFindingIds)
            {
                if (!acceptedFindingIds.Contains(memberFindingId))
                {
                    return Result.Failure(AppError.Conflict(
                        "InvoiceCorrection.ComboReviewInvalidatedByCountChange",
                        $"Approved COMBO disease zone '{zd.SourceProposalId}' references finding '{memberFindingId}' which is no longer valid under corrected pole evidence. Supply replacement COMBO review decisions with the correction."));
                }
            }
        }

        return Result.Success();
    }

    private async Task<Result<ComboInvoicePricing?>> ResolveComboPricingAsync(
        VerifiedInvoiceContext context,
        IEnumerable<SurveyOrderPoleCountSnapshotItem> items,
        CancellationToken cancellationToken,
        Polygon? requestedArea = null)
    {
        if (context.ComboEvidence is null)
            return requestedArea is null
                ? Result.Success<ComboInvoicePricing?>(null)
                : Result.Failure<ComboInvoicePricing?>(AppError.Validation("ComboPricing.ScopeNotApplicable", "A plant health survey area requires COMBO evidence."));

        var (mappingRate, healthRate) = context.CurrentInvoice is { } invoice
            ? (invoice.MappingPricePerPole, invoice.PlantHealthPricePerPole)
            : (context.Commercial.MappingPricePerPole, context.Commercial.PlantHealthPricePerPole);

        // Earlier agreements and their corrections retain the price model booked for that order.
        if (!mappingRate.HasValue && !healthRate.HasValue)
            return requestedArea is null
                ? Result.Success<ComboInvoicePricing?>(null)
                : Result.Failure<ComboInvoicePricing?>(AppError.Conflict("ComboPricing.RatesMissing", "The order needs a pinned component price to use a separate plant health survey area."));
        if (!mappingRate.HasValue || !healthRate.HasValue)
            return Result.Failure<ComboInvoicePricing?>(AppError.Conflict("ComboPricing.RatesMissing", "Both pinned component rates are required."));

        var eligible = items.Where(item => item.IsAccepted && item.IsStrictlyInBoundary && item.PlantProfileId.HasValue);
        var surveyArea = context.CurrentInvoice?.PlantHealthSurveyArea ?? requestedArea;
        if (surveyArea is not null)
        {
            if (!context.Boundary.Geometry.Covers(surveyArea))
                return Result.Failure<ComboInvoicePricing?>(AppError.Validation("ComboPricing.ScopeOutsideBoundary", "The plant health survey area must be inside the approved farm boundary."));

            eligible = eligible.Where(item => surveyArea.Covers(item.EffectivePosition));
        }
        if (context.Order.ScopeType == SurveyScopeType.Zones)
        {
            var zones = await db.Set<SurveyOrderZoneScope>().AsNoTracking()
                .Where(zone => zone.SurveyOrderId == context.Order.Id && zone.FarmId == context.Order.FarmId)
                .Select(zone => zone.BoundarySnapshot).ToListAsync(cancellationToken);
            if (zones.Count == 0 || zones.Any(zone => zone.IsEmpty || !zone.IsValid || zone.SRID != 4326))
                return Result.Failure<ComboInvoicePricing?>(AppError.Conflict("ComboPricing.ScopeMissing", "Valid pinned boundaries for the surveyed zones are required."));

            eligible = eligible.Where(item => zones.Any(zone => zone.Covers(item.EffectivePosition)));
        }

        var healthCount = eligible.Select(item => item.PlantProfileId!.Value).Distinct().Count();
        return Result.Success<ComboInvoicePricing?>(new(healthCount, mappingRate.Value, healthRate.Value, surveyArea));
    }

    // R05E-05: Approver is derived strictly from the authenticated Rank-1 server actor
    private static Result<Guid?> ValidateZeroPoleDecision(
        int verifiedBillablePoleCount,
        Guid actorId,
        string? zeroPoleReasonCode,
        string? zeroPoleEvidenceReference,
        string errorPrefix)
    {
        if (verifiedBillablePoleCount > 0)
        {
            if (zeroPoleReasonCode is not null || zeroPoleEvidenceReference is not null)
            {
                return Result.Failure<Guid?>(AppError.Validation(
                    $"{errorPrefix}.ZeroPoleFieldsNotAllowed",
                    "Zero-pole exception fields must be null when verified billable pole count is greater than zero."));
            }

            return Result.Success<Guid?>(null);
        }

        if (string.IsNullOrWhiteSpace(zeroPoleReasonCode) ||
            string.IsNullOrWhiteSpace(zeroPoleEvidenceReference))
        {
            return Result.Failure<Guid?>(AppError.Validation(
                $"{errorPrefix}.ZeroPoleApprovalRequired",
                "Zero verified billable poles requires explicit ZeroPoleReasonCode and ZeroPoleEvidenceReference."));
        }

        return Result.Success<Guid?>(actorId);
    }

    private async Task AppendOrderStatusHistoryAsync(
        SurveyOrder order,
        SurveyOrderStatus fromStatus,
        Guid actorId,
        string source,
        string reason,
        string evidenceReference,
        Guid correlationId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        Guid? latestHistoryId = null;
        long? maxSequence = null;

        await using (var cmd = CreateCommand("""
            SELECT
                (SELECT id FROM survey.order_state_history
                 WHERE survey_order_id = @order_id
                 ORDER BY transition_sequence DESC NULLS LAST, created_at DESC, id DESC
                 LIMIT 1) AS latest_id,
                (SELECT MAX(transition_sequence) FROM survey.order_state_history
                 WHERE survey_order_id = @order_id) AS max_seq
            """))
        {
            cmd.Parameters.Add(new NpgsqlParameter("order_id", order.Id));
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0)) latestHistoryId = reader.GetGuid(0);
                if (!reader.IsDBNull(1)) maxSequence = reader.GetInt64(1);
            }
        }

        OrderStateHistory history;
        if (latestHistoryId.HasValue)
        {
            var nextSeq = checked((uint)Math.Max(2L, (maxSequence ?? 1L) + 1L));
            history = OrderStateHistory.CreateTransition(
                id: Guid.CreateVersion7(),
                surveyOrderId: order.Id,
                orderVersion: order.Version,
                fromStatus: fromStatus,
                toStatus: order.Status,
                actorId: actorId,
                source: source,
                reason: reason,
                evidenceReference: evidenceReference,
                correlationId: correlationId,
                predecessorHistoryId: latestHistoryId.Value,
                createdAt: createdAt,
                transitionSequence: nextSeq);
        }
        else
        {
            history = OrderStateHistory.CreateInitial(
                id: Guid.CreateVersion7(),
                surveyOrderId: order.Id,
                orderVersion: order.Version,
                initialStatus: order.Status,
                actorId: actorId,
                source: source,
                reason: reason,
                correlationId: correlationId,
                createdAt: createdAt,
                transitionSequence: 1u);
        }

        db.Set<OrderStateHistory>().Add(history);
    }

    private void AddStateChangedOutbox(
        SurveyOrder order,
        Guid actorId,
        Guid correlationId,
        string changeType,
        DateTimeOffset changedAt,
        Guid attemptId,
        Guid missionId)
    {
        var payload = new SurveyCaptureStateChangedV1(
            order.Id,
            order.TenantId,
            order.FarmId,
            order.Status.ToString(),
            changeType,
            attemptId,
            missionId,
            null,
            null,
            changedAt);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyCaptureStateChangedV1,
            Guid.CreateVersion7(),
            correlationId,
            order.TenantId,
            actorId,
            changedAt,
            payload);
        db.Set<OutboxMessage>().Add(outboxFactory.Create(
            envelope,
            IntegrationEventTypes.SurveyCaptureStateChangedV1,
            order.Id.ToString("D")));
    }

    private void AddReleaseEvaluationOutbox(
        SurveyOrder order,
        SurveyInvoiceRevision invoice,
        SurveyInternalApprovedResultRevision approval,
        SurveyOrderPoleCountSnapshot snapshot,
        SurveyReleaseTriggerSource source,
        Guid operationId,
        string requestFingerprint,
        Guid actorId,
        Guid correlationId,
        DateTimeOffset occurredAt)
    {
        var trigger = new SurveyReleaseEvaluationRequestedV1(
            order.Id,
            order.TenantId,
            order.FarmId,
            source.ToString(),
            operationId,
            requestFingerprint,
            occurredAt,
            ExpectedOrderVersion: order.Version,
            ExpectedApprovedResultRevisionId: approval.Id,
            ExpectedApprovedResultFingerprint: approval.EvidenceFingerprint,
            ExpectedCountSnapshotId: snapshot.Id,
            ExpectedCountSnapshotFingerprint: snapshot.EvidenceFingerprint,
            ExpectedInvoiceRevisionId: invoice.Id,
            ExpectedInvoiceRevisionNumber: invoice.RevisionNumber,
            ExpectedInvoiceFingerprint: invoice.EvidenceFingerprint,
            ExpectedCommercialPolicyVersionId: invoice.CommercialPolicyVersionId,
            ExpectedSurveyServicePriceId: invoice.SurveyServicePriceId,
            ExpectedPricePerPole: invoice.PricePerPole,
            ExpectedCurrency: invoice.Currency,
            TriggerActorId: actorId);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyReleaseEvaluationRequestedV1,
            Guid.CreateVersion7(), correlationId, order.TenantId, actorId, occurredAt, trigger);
        db.Set<OutboxMessage>().Add(outboxFactory.Create(
            envelope, envelope.EventType, order.Id.ToString("D")));
    }

    private async Task<Result<T>?> TryLoadReplayAsync<T>(
        Guid tenantId,
        Guid orderId,
        Guid actorId,
        string action,
        Guid operationId,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var existingOp = await db.Set<SurveyResultReviewOperation>().AsNoTracking()
            .SingleOrDefaultAsync(op =>
                op.TenantId == tenantId &&
                op.ActorId == actorId &&
                op.Action == action &&
                op.ApiVersion == SurveyResultReviewOperation.ApiVersionV3 &&
                op.OperationId == operationId,
                cancellationToken);

        if (existingOp is null)
            return null;

        if (existingOp.SurveyOrderId != orderId ||
            !string.Equals(existingOp.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return Result.Failure<T>(AppError.Conflict(
                "Operation.Conflict",
                "OperationId was already used with a different request payload or order."));
        }

        var replayed = existingOp.Outcome.Deserialize<T>(PayloadJsonOptions)
            ?? throw new InvalidOperationException("Stored operation receipt outcome cannot be deserialized.");
        return Result.Success(replayed);
    }

    private Task<OrderScopeRow?> LoadOrderScopeAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Set<SurveyOrder>().AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new OrderScopeRow(o.Id, o.TenantId, o.FarmId, o.PreviousCompatibleOrderId))
            .SingleOrDefaultAsync(cancellationToken);

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

    private async Task<bool> IsPointStrictlyInsideBoundaryAsync(
        Guid tenantId,
        Guid farmId,
        Guid boundaryVersionId,
        Point position,
        CancellationToken cancellationToken)
    {
        return await db.Database.SqlQuery<bool>($"""
            SELECT COALESCE(ST_Within(
                ST_SetSRID(ST_MakePoint({position.X}, {position.Y}), 4326),
                geometry), FALSE) AS "Value"
            FROM farm.farm_boundaries
            WHERE id = {boundaryVersionId}
              AND tenant_id = {tenantId}
              AND farm_id = {farmId}
            """).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsAssignedManagerAsync(
        Guid tenantId,
        Guid farmId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return false;

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

    private static AppError MapPersistenceException(string operation, Exception ex)
    {
        if (ex is SurveyDomainException domainEx)
            return AppError.Conflict($"{operation}.{domainEx.Code}", domainEx.Message);

        return AppError.Conflict($"{operation}.ConcurrencyConflict",
            $"Concurrent state change or database constraint conflict occurred during {operation}: {ex.GetBaseException().Message}");
    }
}
