using System.Data;
using System.Text.Json;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Notifications.Domain.Notifications;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Surveys;

/// <summary>
/// Isolated transaction coordinator for V3 settlement evaluation and release.
/// Enforces ADR 0020 Rank 1 authorization BEFORE reading receipts or release artifacts,
/// locks related Orders at Rank 2, and rechecks exact V3-05F tokens and funding at Rank 5/6.
/// </summary>
internal sealed class SurveySettlementReleaseCoordinator(
    AgriDroneSchemaDbContext db,
    SurveyResultReviewTransaction resultReview,
    IV3BaselineDeliveryProofReader baselineDeliveryProofReader,
    V3SurveyReleaseStore releaseStore,
    OutboxMessageFactory outboxFactory,
    TimeProvider timeProvider) : ISurveySettlementReleaseCoordinator
{
    private sealed record OrderDiscoveryRow(
        Guid Id,
        Guid TenantId,
        Guid FarmId,
        Guid? PreviousCompatibleOrderId,
        Guid? CurrentApprovedResultRevisionId);

    public async Task<EvaluateSurveySettlementReleaseResult> EvaluateAndReleaseAsync(
        EvaluateSurveySettlementReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "ISurveySettlementReleaseCoordinator must own its own isolated transaction and cannot run inside an outer SQL transaction.");
        }

        if (request.OrderId == Guid.Empty)
        {
            return EvaluateSurveySettlementReleaseResult.NotFound(
                request.OrderId,
                "OrderId.Required",
                "Survey order identifier is required.");
        }

        if (!Enum.IsDefined(request.TriggerSource))
        {
            return EvaluateSurveySettlementReleaseResult.Blocked(
                request.OrderId, "SettlementRelease.InvalidTriggerSource", "The release trigger source is unsupported.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var discovery = await db.Set<SurveyOrder>().AsNoTracking()
                .Where(o => o.Id == request.OrderId)
                .Select(o => new OrderDiscoveryRow(
                    o.Id,
                    o.TenantId,
                    o.FarmId,
                    o.PreviousCompatibleOrderId,
                    o.CurrentApprovedResultRevisionId))
                .SingleOrDefaultAsync(cancellationToken);

            if (discovery is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.NotFound(request.OrderId);
            }

            if (request.ExpectedTenantId.HasValue && request.ExpectedTenantId.Value != discovery.TenantId)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.Forbidden(
                    request.OrderId,
                    "SettlementRelease.CrossTenantForbidden",
                    "Order does not belong to the expected tenant.");
            }

            if (request.ExpectedFarmId is Guid farmId && farmId != discovery.FarmId)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.Forbidden(
                    request.OrderId, "SettlementRelease.CrossFarmForbidden", "Order does not belong to the expected farm.");
            }

            // Discover current approval actor candidate (without trusting it until locked at Rank 6)
            Guid? discoveredApprovalActorId = discovery.CurrentApprovedResultRevisionId.HasValue
                ? await db.Set<SurveyInternalApprovedResultRevision>().AsNoTracking()
                    .Where(a =>
                        a.Id == discovery.CurrentApprovedResultRevisionId.Value &&
                        a.SurveyOrderId == discovery.Id)
                    .Select(a => (Guid?)a.ReviewedBy)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;

            // ADR 0020 Rank 1: Authorize BEFORE reading receipts or release artifacts
            var rank1Result = await AuthorizeAtRank1Async(request, discovery, discoveredApprovalActorId, cancellationToken);
            if (!rank1Result.Authorized)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.Forbidden(
                    request.OrderId,
                    rank1Result.ErrorCode!,
                    rank1Result.ErrorMessage!);
            }

            var owner = await ReadOwnerAsync(discovery, cancellationToken);
            if (owner is null)
            {
                return EvaluateSurveySettlementReleaseResult.Blocked(
                    discovery.Id, "SettlementRelease.OwnerUnavailable", "An active owner is required for the release notice.");
            }

            // ADR 0020 Rank 2: Lock related Orders in ascending ID order (Current FOR UPDATE, Baseline source FOR SHARE)
            var order = await LockRelatedOrdersAsync(discovery, cancellationToken);
            if (order is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.NotFound(request.OrderId);
            }

            if (order.TenantId != discovery.TenantId || order.FarmId != discovery.FarmId)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.Forbidden(request.OrderId);
            }

            if (order.Scope != ContractScope.V3)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.Blocked(
                    order.Id,
                    "SurveyOrder.ScopeInvalid",
                    "Only V3 orders can be evaluated by the V3 settlement release coordinator.",
                    order.Version,
                    order.Status.ToString());
            }

            if (order.Status == SurveyOrderStatus.Cancelled)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.Blocked(
                    order.Id,
                    "SurveyOrder.Cancelled",
                    "Cancelled orders cannot be released.",
                    order.Version,
                    order.Status.ToString());
            }

            if (order.Status == SurveyOrderStatus.Completed)
            {
                var previousReceipt = await releaseStore.ReadReceiptAsync(order.Id, order.TenantId, cancellationToken);
                if (previousReceipt is not null)
                {
                    if ((request.ExpectedInvoiceRevisionId is Guid invoiceId && invoiceId != previousReceipt.InvoiceRevisionId) ||
                        (request.ExpectedApprovedResultRevisionId is Guid approvalId && approvalId != previousReceipt.ApprovedResultRevisionId))
                    {
                        return EvaluateSurveySettlementReleaseResult.StaleContext(
                            order.Id, "SettlementRelease.StaleRevisionTokens", "The order already has a different released revision.",
                            order.Version, order.Status.ToString());
                    }

                    return previousReceipt;
                }
            }

            if (request.ExpectedOrderVersion.HasValue && order.Version != request.ExpectedOrderVersion.Value)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.StaleContext(
                    order.Id,
                    "SurveyOrder.VersionConflict",
                    $"Expected Order version {request.ExpectedOrderVersion.Value} but found {order.Version}.",
                    order.Version,
                    order.Status.ToString());
            }

            if (order.Status != SurveyOrderStatus.AwaitingSettlement)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.StaleContext(
                    order.Id,
                    "SurveyOrder.StatusInvalid",
                    $"Order must be in AwaitingSettlement status to evaluate release (current: {order.Status}).",
                    order.Version,
                    order.Status.ToString());
            }

            if (!order.CurrentInvoiceRevisionId.HasValue ||
                !order.CurrentApprovedResultRevisionId.HasValue ||
                !order.VerifiedPoleCountSnapshotId.HasValue)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.StaleContext(
                    order.Id,
                    "Invoice.NotFinalized",
                    "Order does not have current invoice, approval, and count snapshot pointers.",
                    order.Version,
                    order.Status.ToString());
            }

            // ADR 0020 Rank 5: Lock DepositObligation, Payments, and Funding tables
            SurveyDepositObligation? depositObligation = null;
            if (order.DepositObligationId.HasValue)
            {
                depositObligation = await db.Set<SurveyDepositObligation>()
                    .FromSqlInterpolated($"SELECT * FROM survey.survey_deposit_obligations WHERE id = {order.DepositObligationId.Value} AND survey_order_id = {order.Id} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
                if (depositObligation is null || depositObligation.Status == SurveyDepositObligationStatus.Cancelled)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return EvaluateSurveySettlementReleaseResult.ReconciliationHold(
                        order.Id,
                        "DepositObligation.Invalid",
                        "Order deposit obligation is missing or cancelled.",
                        order.Version,
                        order.Status.ToString(),
                        order.CurrentInvoiceRevisionId);
                }
            }

            var orderPayments = await db.Set<SurveyPayment>()
                .FromSqlInterpolated($"""
                    SELECT payment.*, payment.xmin FROM survey.survey_payments AS payment
                    WHERE payment.survey_order_id = {order.Id}
                    ORDER BY payment.id
                    FOR SHARE
                    """)
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var fundingResult = await SurveyInvoiceFunding.ReadAsync(
                db,
                order,
                depositObligation,
                currentInvoiceRevisionId: order.CurrentInvoiceRevisionId,
                lockRows: true,
                cancellationToken,
                preloadedOrderPayments: orderPayments);
            if (fundingResult.IsFailure)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.ReconciliationHold(
                    order.Id,
                    fundingResult.Error.Code,
                    fundingResult.Error.Description,
                    order.Version,
                    order.Status.ToString(),
                    order.CurrentInvoiceRevisionId);
            }

            var hasCancellation = await db.Set<SurveyCancellationDecision>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_cancellation_decisions WHERE survey_order_id = {order.Id} FOR SHARE")
                .AnyAsync(cancellationToken);
            if (hasCancellation)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.Blocked(
                    order.Id,
                    "SurveyOrder.Cancelled",
                    "An existing cancellation decision blocks release.",
                    order.Version,
                    order.Status.ToString());
            }

            // ADR 0020 Rank 6: Lock and verify exact InvoiceRevision, ApprovedResult, CountSnapshot, and Baseline proof
            var invoice = await db.Set<SurveyInvoiceRevision>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_invoice_revisions WHERE id = {order.CurrentInvoiceRevisionId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            var approval = await db.Set<SurveyInternalApprovedResultRevision>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_internal_approved_result_revisions WHERE id = {order.CurrentApprovedResultRevisionId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            var snapshot = await db.Set<SurveyOrderPoleCountSnapshot>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_order_pole_count_snapshots WHERE id = {order.VerifiedPoleCountSnapshotId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            if (invoice is null || approval is null || snapshot is null ||
                invoice.Status != SurveyInvoiceRevision.StatusCurrent ||
                !approval.IsCurrent ||
                invoice.ApprovedResultRevisionId != approval.Id ||
                invoice.PoleCountSnapshotId != snapshot.Id ||
                approval.PoleCountSnapshotId != snapshot.Id)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.StaleContext(
                    order.Id,
                    "InvoiceRevision.ConsistencyConflict",
                    "Current invoice, approved result, or count snapshot pointers are inconsistent.",
                    order.Version,
                    order.Status.ToString(),
                    invoice?.Id,
                    invoice?.RevisionNumber,
                    invoice?.EvidenceFingerprint);
            }

            if (rank1Result.VerifiedManagerActorId.HasValue &&
                approval.ReviewedBy != rank1Result.VerifiedManagerActorId.Value &&
                request.TriggerSource is not (SurveyReleaseTriggerSource.InvoiceFinalization or SurveyReleaseTriggerSource.PreReleaseCorrection))
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.StaleContext(
                    order.Id,
                    "ApprovedResult.ApproverChanged",
                    "Approval actor changed concurrently during evaluation; retry with current revision.",
                    order.Version,
                    order.Status.ToString(),
                    invoice.Id,
                    invoice.RevisionNumber,
                    invoice.EvidenceFingerprint);
            }

            if (!MatchesExpectedTokens(request, invoice, approval, snapshot))
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.StaleContext(
                    order.Id,
                    "SettlementRelease.StaleRevisionTokens",
                    "One or more expected V3-05F revision, snapshot, price, or fingerprint tokens no longer match.",
                    order.Version,
                    order.Status.ToString(),
                    invoice.Id,
                    invoice.RevisionNumber,
                    invoice.EvidenceFingerprint);
            }

            if (!order.RequiresBaselineMapping)
            {
                var proof = await baselineDeliveryProofReader.ReadCurrentAsync(
                    db, order.TenantId, order.FarmId, cancellationToken);
                if (proof is null || !proof.AllowsFollowUp(order.TenantId, order.FarmId) ||
                    proof.BaseMapVersionId != approval.BaselineMapVersionId)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return EvaluateSurveySettlementReleaseResult.Blocked(
                        order.Id,
                        "BaselineDeliveryProof.Unavailable",
                        "Follow-up release requires a valid, non-withdrawn baseline delivery proof.",
                        order.Version,
                        order.Status.ToString());
                }
            }

            var funding = fundingResult.Value;
            if (funding.HasOpenReconciliation)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.ReconciliationHold(
                    order.Id,
                    SurveyInvoiceFunding.HoldErrorCode,
                    string.Join(" ", funding.ReconciliationReasons),
                    order.Version,
                    order.Status.ToString(),
                    invoice.Id);
            }

            if (funding.ActiveOtherRefundReservations > 0m)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.ReconciliationHold(
                    order.Id,
                    "SettlementRelease.ConflictingActiveRefundReservations",
                    "Active refund reservations for another refund origin block release until reconciled.",
                    order.Version,
                    order.Status.ToString(),
                    invoice.Id);
            }

            var settlement = funding.ComputeInvoiceSettlement(invoice.TotalAmount);
            if (settlement.DueAmount > 0m)
            {
                await tx.RollbackAsync(cancellationToken);
                return EvaluateSurveySettlementReleaseResult.InsufficientFunding(
                    orderId: order.Id,
                    orderVersion: order.Version,
                    orderStatus: order.Status.ToString(),
                    approvedResultRevisionId: approval.Id,
                    approvedResultFingerprint: approval.EvidenceFingerprint,
                    countSnapshotId: snapshot.Id,
                    countSnapshotFingerprint: snapshot.EvidenceFingerprint,
                    invoiceRevisionId: invoice.Id,
                    invoiceRevisionNumber: invoice.RevisionNumber,
                    invoiceFingerprint: invoice.EvidenceFingerprint,
                    totalAmount: invoice.TotalAmount,
                    validAllocatedDepositNet: funding.ValidAllocatedDepositNet,
                    confirmedNetReceipts: funding.ConfirmedNetPaid,
                    dueAmount: settlement.DueAmount,
                    currency: invoice.Currency);
            }

            SurveyOverpaymentReservationPlan? overpayment = null;
            if (settlement.ExcessAmount > 0m)
            {
                var planResult = funding.PlanOverpaymentReservations(
                    invoice.TotalAmount,
                    originDecisionId: invoice.Id,
                    invoice.Currency);
                if (planResult.IsFailure)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return EvaluateSurveySettlementReleaseResult.ReconciliationHold(
                        order.Id,
                        planResult.Error.Code,
                        planResult.Error.Description,
                        order.Version,
                        order.Status.ToString(),
                        invoice.Id);
                }
                overpayment = planResult.Value;
            }

            var commercial = await db.Set<SurveyOrderCommercialSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(c => c.SurveyOrderId == order.Id, cancellationToken);
            var claimPolicy = commercial is null ? null : await db.Set<ClaimPolicy>().AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == commercial.ClaimPolicyId, cancellationToken);
            if (commercial is null || claimPolicy is null ||
                claimPolicy.VersionNumber != commercial.ClaimPolicyVersionNumber ||
                claimPolicy.CalendarWindowDays != commercial.ClaimWindowDays ||
                claimPolicy.TimeZoneId != commercial.ClaimTimeZoneId ||
                claimPolicy.ExcludeNoticeDate != commercial.ExcludeNoticeDate ||
                commercial.ServiceCode != approval.ServiceCode || commercial.ScreensDisease != approval.ScreensDisease)
            {
                return EvaluateSurveySettlementReleaseResult.Blocked(
                    order.Id, "SettlementRelease.PolicyUnavailable", "The pinned service or claim policy cannot be verified.");
            }

            // Serialize allocation of the current farm map across different orders.
            var lockKey = $"farm-base-map-release:{order.TenantId:D}:{order.FarmId:D}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
            var map = await db.Set<FarmBaseMapVersion>().SingleOrDefaultAsync(m =>
                m.Id == approval.FarmBaseMapVersionId && m.TenantId == order.TenantId && m.FarmId == order.FarmId &&
                m.SourceSurveyOrderId == order.Id && m.SourceMissionId == approval.MissionId &&
                m.FarmBoundaryVersionId == approval.FarmBoundaryVersionId, cancellationToken);
            if (map is null || map.Status != FarmBaseMapStatus.InternalApproved ||
                map.IsBaseline != order.RequiresBaselineMapping ||
                (map.IsBaseline ? map.Id : map.BaselineMapVersionId) != approval.BaselineMapVersionId)
            {
                return EvaluateSurveySettlementReleaseResult.StaleContext(
                    order.Id, "SettlementRelease.MapUnavailable", "The exact approved capture map could not be verified.");
            }

            var now = SurveyInternalApprovedResultRevision.NormalizeUtcToMicroseconds(timeProvider.GetUtcNow());
            var previous = await db.Set<FarmBaseMapVersion>().SingleOrDefaultAsync(m =>
                m.TenantId == order.TenantId && m.FarmId == order.FarmId &&
                m.Status == FarmBaseMapStatus.Published, cancellationToken);
            var historicalMap = previous is not null && previous.VersionNumber > map.VersionNumber;
            if (previous is not null && !historicalMap)
            {
                previous.Supersede(now);
                // The farm's partial unique index must see the superseded row first.
                await db.SaveChangesAsync(cancellationToken);
            }

            var publicationId = Guid.CreateVersion7();
            var deadline = claimPolicy.CalculateExclusiveDeadline(now);
            var notice = Notification.ForV3SurveyRelease(owner.Id, order.TenantId, order.FarmId, order.Id, now);
            var paidAt = FindCoveredAt(funding, invoice);
            var proofAtRelease = new V3ResultReleaseProof(
                order.Id, order.TenantId, order.FarmId, publicationId, 1, null,
                false, false, false, null, approval.Id, approval.EvidenceFingerprint, invoice.Id,
                invoice.EvidenceFingerprint, approval.ServiceCode, approval.ScreensDisease,
                map.Id, approval.BaselineMapVersionId, approval.ComboReviewRevisionId, invoice.TotalAmount,
                invoice.Currency, paidAt, now, now, now, publicationId, deadline, claimPolicy.Id);

            var mediaIds = await ReadReleaseMediaAsync(approval, cancellationToken);
            var reservations = overpayment?.CreateObligations(order.Id, order.TenantId, now) ?? [];
            db.Set<SurveyRefundObligation>().AddRange(reservations);
            map.Publish(approval.ReviewedBy, now);
            if (historicalMap)
                map.Supersede(now);
            order.TransitionToCompletedV3(now);
            db.Set<Notification>().Add(notice);
            AddReleaseEmail(owner, order, notice.Id, request.CorrelationId, now);
            using var audit = JsonSerializer.SerializeToDocument(new { PublicationId = publicationId, InvoiceId = invoice.Id, Deadline = deadline });
            db.Set<AuditLog>().Add(AuditLog.ForIntegrationAction(order.TenantId, order.FarmId, publicationId,
                request.CorrelationId, "SurveyOrder", order.Id, "SurveyResult.ReleasedV3", audit, now));
            await AddCompletionHistoryAsync(order, approval.ReviewedBy, publicationId, request.CorrelationId, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            var receipt = EvaluateSurveySettlementReleaseResult.Released(
                order.Id, order.Version, order.Status.ToString(), approval.Id, approval.EvidenceFingerprint,
                snapshot.Id, snapshot.EvidenceFingerprint, invoice.Id, invoice.RevisionNumber,
                invoice.EvidenceFingerprint, invoice.TotalAmount, funding.ValidAllocatedDepositNet,
                funding.ConfirmedNetPaid, settlement.ExcessAmount,
                reservations.Count > 0 ? reservations[0].Id :
                    overpayment is { ExistingObligations.Count: > 0 } ? overpayment.ExistingObligations[0].Id : null,
                invoice.Currency, publicationId, proofAtRelease);
            await releaseStore.InsertAsync(receipt, notice.Id, mediaIds, cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO survey.financial_report_history(source,source_id,order_id,data)
                VALUES ('original_service_publication', {publicationId}, {order.Id},
                    jsonb_build_object('published_at', {now}, 'amount', {invoice.TotalAmount}))
                ON CONFLICT (order_id) WHERE source = 'original_service_publication' DO NOTHING
                """, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return receipt;
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected or
            PostgresErrorCodes.LockNotAvailable)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return EvaluateSurveySettlementReleaseResult.StaleContext(
                request.OrderId,
                "SettlementRelease.ConcurrencyConflict",
                "Settlement release evaluation conflicted with a concurrent transaction and should be retried.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            (pg.SqlState is PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.LockNotAvailable ||
             (pg.SqlState == PostgresErrorCodes.UniqueViolation &&
                pg.ConstraintName == "uq_farm_base_map_versions_one_published")))
        {
            await tx.RollbackAsync(CancellationToken.None);
            return EvaluateSurveySettlementReleaseResult.StaleContext(
                request.OrderId,
                "SettlementRelease.ConcurrencyConflict",
                "Settlement release evaluation conflicted with a concurrent transaction and should be retried.");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private sealed record ReleaseOwner(Guid Id, string Email);

    private async Task<ReleaseOwner?> ReadOwnerAsync(OrderDiscoveryRow order, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<ReleaseOwner>($"""
            SELECT u.id AS "Id", u.email AS "Email"
            FROM identity.tenant_memberships tm
            JOIN identity.users u ON u.id = tm.user_id
            JOIN identity.tenants t ON t.id = tm.tenant_id
            JOIN farm.farms f ON f.tenant_id = t.id
            WHERE tm.tenant_id = {order.TenantId} AND f.id = {order.FarmId}
              AND tm.role = 'OWNER' AND tm.status = 'ACTIVE'
              AND u.status = 'ACTIVE' AND u.deleted_at IS NULL
              AND t.status = 'ACTIVE' AND t.deleted_at IS NULL
              AND f.status = 'ACTIVE' AND f.deleted_at IS NULL
            FOR SHARE OF tm, u, t, f
            """).SingleOrDefaultAsync(cancellationToken);

    private static DateTimeOffset FindCoveredAt(SurveyInvoiceFundingSummary funding, SurveyInvoiceRevision invoice)
    {
        if (invoice.TotalAmount == 0m)
            return invoice.FinalizedAt;

        var remaining = invoice.TotalAmount;
        foreach (var entry in funding.Receipts
                     .Where(r => r.IsEligibleForCurrentInvoiceSettlement && r.EligibleSettlementAmount > 0m)
                     .OrderBy(r => r.Receipt.ReceivedAt))
        {
            remaining -= entry.EligibleSettlementAmount;
            if (remaining <= 0m)
                return entry.Receipt.ReceivedAt > invoice.FinalizedAt ? entry.Receipt.ReceivedAt : invoice.FinalizedAt;
        }

        throw new InvalidOperationException("The covered invoice does not have enough eligible receipt evidence.");
    }

    private void AddReleaseEmail(
        ReleaseOwner owner, SurveyOrder order, Guid noticeId, Guid correlationId, DateTimeOffset now)
    {
        var message = new EmailNotificationRequestedV1(noticeId, EmailTemplateKeys.V3SurveyResultReleased,
            [new EmailRecipientV1(owner.Email, null)], new Dictionary<string, string>
            {
                [EmailTemplateVariableKeys.OrderNumber] = order.OrderNumber
            });
        var envelope = IntegrationEventEnvelopeFactory.Create(IntegrationEventDescriptors.EmailNotificationRequestedV1,
            noticeId, correlationId, order.TenantId, null, now, message);
        db.Set<OutboxMessage>().Add(outboxFactory.Create(
            envelope, IntegrationEventTypes.EmailNotificationRequestedV1, order.Id.ToString("D")));
    }

    private async Task<Guid[]> ReadReleaseMediaAsync(
        SurveyInternalApprovedResultRevision approval, CancellationToken cancellationToken)
    {
        var reference = await db.Set<CaptureAttempt>().AsNoTracking()
            .Where(a => a.Id == approval.CaptureAttemptId && a.OrderId == approval.SurveyOrderId)
            .Select(a => a.InputApprovalReference).SingleOrDefaultAsync(cancellationToken);
        var rawImages = db.Set<CaptureInputManifestFile>().AsNoTracking().Where(_ => false).Select(f => f.MediaAssetId);
        if (Guid.TryParse(reference, out var inputApprovalId))
        {
            rawImages = from file in db.Set<CaptureInputManifestFile>()
                        join input in db.Set<CaptureInputApproval>() on file.ManifestId equals input.InputManifestId
                        where input.Id == inputApprovalId && input.CaptureAttemptId == approval.CaptureAttemptId &&
                              input.SurveyOrderId == approval.SurveyOrderId && input.MissionId == approval.MissionId &&
                              input.TenantId == approval.TenantId && input.FarmId == approval.FarmId &&
                              (file.MediaRole == "RawImage" || file.MediaRole == "RAW_IMAGE")
                        select file.MediaAssetId;
        }

        var media = await db.Set<MissionMedia>().AsNoTracking()
            .Where(m => m.MissionId == approval.MissionId && m.Media.TenantId == approval.TenantId &&
                m.Media.FarmId == approval.FarmId && m.Media.DeletedAt == null &&
                m.Media.StorageStatus == MediaStorageStatus.Active &&
                (m.MediaRole == MissionMediaRole.ProcessedImage || m.MediaRole == MissionMediaRole.Thumbnail ||
                 (m.MediaRole == MissionMediaRole.RawImage && rawImages.Contains(m.MediaId))) &&
                (m.Media.MimeType == "image/jpeg" || m.Media.MimeType == "image/png" || m.Media.MimeType == "image/tiff"))
            .Select(m => m.MediaId).Distinct().OrderBy(id => id).ToArrayAsync(cancellationToken);
        return media;
    }

    private async Task AddCompletionHistoryAsync(
        SurveyOrder order, Guid actorId, Guid publicationId, Guid correlationId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var predecessor = await db.Set<OrderStateHistory>().AsNoTracking()
            .Where(h => h.SurveyOrderId == order.Id)
            .OrderByDescending(h => h.TransitionSequence.HasValue)
            .ThenByDescending(h => h.TransitionSequence)
            .ThenByDescending(h => h.CreatedAt).ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        const string source = "V3_SETTLEMENT_RELEASE";
        const string reason = "Finalized invoice settled and reviewed result released";
        var history = predecessor is null
            ? OrderStateHistory.CreateInitial(Guid.CreateVersion7(), order.Id, order.Version,
                order.Status, actorId, source, reason, correlationId, now, 1)
            : OrderStateHistory.CreateTransition(Guid.CreateVersion7(), order.Id, order.Version,
                SurveyOrderStatus.AwaitingSettlement, order.Status, actorId, source, reason,
                publicationId.ToString("D"), correlationId, predecessor.Id, now,
                checked((predecessor.TransitionSequence ?? 1u) + 1u));
        db.Set<OrderStateHistory>().Add(history);
    }

    private async Task<(bool Authorized, Guid? VerifiedManagerActorId, string? ErrorCode, string? ErrorMessage)> AuthorizeAtRank1Async(
        EvaluateSurveySettlementReleaseRequest request,
        OrderDiscoveryRow discovery,
        Guid? discoveredApprovalActorId,
        CancellationToken cancellationToken)
    {
        if (request.TriggerSource is SurveyReleaseTriggerSource.InvoiceFinalization or SurveyReleaseTriggerSource.PreReleaseCorrection)
        {
            if (request.TriggerActorId is not Guid managerActorId || managerActorId == Guid.Empty)
            {
                return (false, null, "SettlementRelease.Forbidden", "Manager actor is required to trigger settlement release.");
            }

            if (!await resultReview.IsAssignedManagerAsync(discovery.TenantId, discovery.FarmId, managerActorId, cancellationToken))
            {
                return (false, null, "SettlementRelease.ManagerForbidden", "Actor is not an active assigned Manager for this farm.");
            }

            return (true, managerActorId, null, null);
        }

        if (request.TriggerSource == SurveyReleaseTriggerSource.AdminReconciliation)
        {
            if (request.TriggerActorId is not Guid adminActorId || adminActorId == Guid.Empty ||
                !await IsActiveSystemAdminAsync(discovery.TenantId, adminActorId, cancellationToken))
            {
                return (false, null, "SettlementRelease.Forbidden", "Active SystemAdmin and Tenant/Farm context are required for reconciliation release.");
            }
        }

        if (discoveredApprovalActorId is Guid approvalActorId)
        {
            if (!await resultReview.IsAssignedManagerAsync(discovery.TenantId, discovery.FarmId, approvalActorId, cancellationToken))
                return (false, null, "SettlementRelease.ApprovalAssignmentRevoked", "The approving Manager assignment is no longer active for this farm.");

            return (true, approvalActorId, null, null);
        }

        return await IsActiveTenantAndFarmAsync(discovery.TenantId, discovery.FarmId, cancellationToken)
            ? (true, null, null, null)
            : (false, null, "SettlementRelease.TenantOrFarmInactive", "Tenant or Farm is not active.");
    }

    private async Task<bool> IsActiveTenantAndFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return false;

        var matched = await db.Database.SqlQuery<Guid>($"""
            SELECT farm.id AS "Value"
            FROM identity.tenants tenant
            JOIN farm.farms farm
              ON farm.id = {farmId}
             AND farm.tenant_id = tenant.id
             AND farm.status = 'ACTIVE'::system.general_status
             AND farm.deleted_at IS NULL
            WHERE tenant.id = {tenantId}
              AND tenant.status = 'ACTIVE'::system.general_status
            FOR SHARE OF tenant, farm
            """).FirstOrDefaultAsync(cancellationToken);

        return matched == farmId;
    }

    private async Task<bool> IsActiveSystemAdminAsync(
        Guid tenantId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return false;

        var activeTenant = await db.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value"
            FROM identity.tenants
            WHERE id = {tenantId} AND status = 'ACTIVE'::system.general_status
            FOR SHARE
            """).FirstOrDefaultAsync(cancellationToken);
        if (activeTenant != tenantId)
            return false;

        var matched = await db.Database.SqlQuery<Guid>($"""
            SELECT app_user.id AS "Value"
            FROM identity.users app_user
            JOIN identity.user_roles user_role ON user_role.user_id = app_user.id
            JOIN identity.roles role ON role.id = user_role.role_id
            WHERE app_user.id = {actorId}
              AND app_user.status = 'ACTIVE'::system.user_status
              AND role.code = 'SYSTEM_ADMIN'
            FOR SHARE OF app_user, user_role, role
            """).FirstOrDefaultAsync(cancellationToken);

        return matched == actorId;
    }

    private async Task<SurveyOrder?> LockRelatedOrdersAsync(
        OrderDiscoveryRow discovery,
        CancellationToken cancellationToken)
    {
        if (discovery.PreviousCompatibleOrderId is Guid baselineOrderId && baselineOrderId != discovery.Id)
        {
            var ids = new[] { baselineOrderId, discovery.Id }.OrderBy(id => id).ToArray();
            foreach (var id in ids)
            {
                if (id == discovery.Id)
                {
                    _ = await db.Set<SurveyOrder>()
                        .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {id} FOR UPDATE")
                        .SingleOrDefaultAsync(cancellationToken);
                }
                else
                {
                    _ = await db.Set<SurveyOrder>()
                        .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {id} FOR SHARE")
                        .SingleOrDefaultAsync(cancellationToken);
                }
            }

            return db.ChangeTracker.Entries<SurveyOrder>()
                .Select(e => e.Entity)
                .SingleOrDefault(o => o.Id == discovery.Id);
        }

        return await db.Set<SurveyOrder>()
            .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {discovery.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static bool MatchesExpectedTokens(
        EvaluateSurveySettlementReleaseRequest request,
        SurveyInvoiceRevision invoice,
        SurveyInternalApprovedResultRevision approval,
        SurveyOrderPoleCountSnapshot snapshot)
    {
        if (request.ExpectedInvoiceRevisionId.HasValue && request.ExpectedInvoiceRevisionId.Value != invoice.Id)
            return false;
        if (request.ExpectedInvoiceRevisionNumber.HasValue && request.ExpectedInvoiceRevisionNumber.Value != invoice.RevisionNumber)
            return false;
        if (!string.IsNullOrWhiteSpace(request.ExpectedInvoiceFingerprint) &&
            !string.Equals(invoice.EvidenceFingerprint, request.ExpectedInvoiceFingerprint.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            return false;
        if (request.ExpectedApprovedResultRevisionId.HasValue && request.ExpectedApprovedResultRevisionId.Value != approval.Id)
            return false;
        if (!string.IsNullOrWhiteSpace(request.ExpectedApprovedResultFingerprint) &&
            !string.Equals(approval.EvidenceFingerprint, request.ExpectedApprovedResultFingerprint.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            return false;
        if (request.ExpectedCountSnapshotId.HasValue && request.ExpectedCountSnapshotId.Value != snapshot.Id)
            return false;
        if (!string.IsNullOrWhiteSpace(request.ExpectedCountSnapshotFingerprint) &&
            !string.Equals(snapshot.EvidenceFingerprint, request.ExpectedCountSnapshotFingerprint.Trim().ToLowerInvariant(), StringComparison.Ordinal))
            return false;
        if (request.ExpectedCommercialPolicyVersionId.HasValue && request.ExpectedCommercialPolicyVersionId.Value != invoice.CommercialPolicyVersionId)
            return false;
        if (request.ExpectedSurveyServicePriceId.HasValue && request.ExpectedSurveyServicePriceId.Value != invoice.SurveyServicePriceId)
            return false;
        if (request.ExpectedPricePerPole.HasValue && request.ExpectedPricePerPole.Value != invoice.PricePerPole)
            return false;
        if (!string.IsNullOrWhiteSpace(request.ExpectedCurrency) &&
            !string.Equals(invoice.Currency, request.ExpectedCurrency.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
