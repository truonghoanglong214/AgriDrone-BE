using System.Data;
using AgriDrone.IntegrationContracts.Surveys;
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
    IV3BaselineDeliveryProofReader baselineDeliveryProofReader) : ISurveySettlementReleaseCoordinator
{
    internal const string ReleasePersistencePendingCode = "ReleasePersistence.PendingA2_4";

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
                if (proof is null || !proof.AllowsFollowUp(order.TenantId, order.FarmId))
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
            }

            await tx.RollbackAsync(cancellationToken);
            return EvaluateSurveySettlementReleaseResult.Blocked(
                order.Id,
                ReleasePersistencePendingCode,
                "Release evaluation passed Rank 1-6 checks; durable release receipt, notice, claim window, and overpayment reservation persistence are completed in V3-06 A2.4.",
                order.Version,
                order.Status.ToString());
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
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState is
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
        finally
        {
            db.ChangeTracker.Clear();
        }
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
