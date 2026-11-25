using System.Data;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Surveys.Application.Abstractions.Invoices;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.GetManagerSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.GetTenantOwnerSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyInvoiceQueries(
    AgriDroneSchemaDbContext db,
    SurveyResultReviewTransaction resultReview) : ISurveyInvoiceQueries
{
    private sealed record OrderScopeRow(Guid Id, Guid TenantId, Guid FarmId);

    public async Task<Result<ManagerSurveyOrderInvoiceResponse>> GetManagerInvoiceAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
            return Result.Failure<ManagerSurveyOrderInvoiceResponse>(AppError.Validation("OrderId.Required", "OrderId is required."));
        if (actorId == Guid.Empty)
            return Result.Failure<ManagerSurveyOrderInvoiceResponse>(AppError.Unauthorized("Invoice.Unauthorized", "Valid actor is required."));

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            var scope = await db.Set<SurveyOrder>().AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => new OrderScopeRow(o.Id, o.TenantId, o.FarmId))
                .SingleOrDefaultAsync(cancellationToken);

            if (scope is null)
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

            // ADR 0020 Rank 1: Verify active assigned Manager BEFORE reading invoice details
            if (!await resultReview.IsAssignedManagerAsync(scope.TenantId, scope.FarmId, actorId, cancellationToken))
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(AppError.Forbidden("Invoice.ManagerForbidden", "The actor is not an active assigned Manager for this farm."));

            var order = await db.Set<SurveyOrder>().AsNoTracking()
                .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            if (order is null)
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(AppError.NotFound("Order.NotFound", "Survey order was not found."));

            if (order.Scope != ContractScope.V3)
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(AppError.Conflict("SurveyOrder.ScopeInvalid", "Only V3 orders are supported by this invoice endpoint."));

            if (!order.CurrentInvoiceRevisionId.HasValue)
            {
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(
                    AppError.Conflict("Invoice.NotFinalized", "Order does not have a finalized current invoice revision yet. Reload result review and finalize first."));
            }

            if (!order.CurrentApprovedResultRevisionId.HasValue || !order.VerifiedPoleCountSnapshotId.HasValue)
            {
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(
                    AppError.Conflict("Invoice.ContextIncomplete", "Current invoice pointers are missing required approval or count snapshot references."));
            }

            var commercial = await db.Set<SurveyOrderCommercialSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(c => c.SurveyOrderId == order.Id, cancellationToken);
            if (commercial is null)
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(AppError.Conflict("CommercialSnapshot.Missing", "Pinned commercial snapshot was not found."));

            var invoice = await db.Set<SurveyInvoiceRevision>().AsNoTracking()
                .SingleOrDefaultAsync(r =>
                    r.Id == order.CurrentInvoiceRevisionId.Value &&
                    r.SurveyOrderId == order.Id &&
                    r.TenantId == order.TenantId &&
                    r.FarmId == order.FarmId &&
                    r.Status == SurveyInvoiceRevision.StatusCurrent,
                    cancellationToken);

            var approval = await db.Set<SurveyInternalApprovedResultRevision>().AsNoTracking()
                .SingleOrDefaultAsync(a =>
                    a.Id == order.CurrentApprovedResultRevisionId.Value &&
                    a.SurveyOrderId == order.Id &&
                    a.TenantId == order.TenantId &&
                    a.FarmId == order.FarmId &&
                    a.IsCurrent,
                    cancellationToken);

            var snapshot = await db.Set<SurveyOrderPoleCountSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(s =>
                    s.Id == order.VerifiedPoleCountSnapshotId.Value &&
                    s.SurveyOrderId == order.Id &&
                    s.TenantId == order.TenantId &&
                    s.FarmId == order.FarmId,
                    cancellationToken);

            if (invoice is null || approval is null || snapshot is null ||
                invoice.ApprovedResultRevisionId != approval.Id ||
                invoice.PoleCountSnapshotId != snapshot.Id ||
                approval.PoleCountSnapshotId != snapshot.Id)
            {
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(
                    AppError.Conflict("InvoiceRevision.ConsistencyConflict", "Current invoice revision does not match current approval or count snapshot pointers."));
            }

            Guid? comboBatchId = approval.ComboBatchId;
            Guid? comboReviewRevisionId = approval.ComboReviewRevisionId;
            string? comboReviewFingerprint = approval.ComboReviewFingerprint;
            if (approval.ComboReviewRevisionId.HasValue)
            {
                var currentReview = await db.Set<SurveyComboReviewRevision>().AsNoTracking()
                    .SingleOrDefaultAsync(r =>
                        r.Id == approval.ComboReviewRevisionId.Value &&
                        r.SurveyOrderId == order.Id &&
                        r.TenantId == order.TenantId &&
                        r.FarmId == order.FarmId &&
                        r.IsCurrent,
                        cancellationToken);
                if (currentReview is not null)
                {
                    comboReviewFingerprint = currentReview.DecisionFingerprint;
                }
            }

            SurveyDepositObligation? depositObligation = null;
            if (order.DepositObligationId is Guid obligationId)
            {
                depositObligation = await db.Set<SurveyDepositObligation>().AsNoTracking()
                    .SingleOrDefaultAsync(o => o.Id == obligationId && o.SurveyOrderId == order.Id, cancellationToken);
                if (depositObligation is null || depositObligation.Status == SurveyDepositObligationStatus.Cancelled)
                {
                    return Result.Failure<ManagerSurveyOrderInvoiceResponse>(
                        AppError.Conflict("DepositObligation.Invalid", "Deposit obligation is missing or cancelled."));
                }
            }

            var depositPayments = depositObligation is null
                ? new Dictionary<Guid, SurveyPayment>()
                : await db.Set<SurveyPayment>().AsNoTracking()
                    .Where(p => p.SurveyOrderId == order.Id && p.DepositObligationId == depositObligation.Id)
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

            var funding = await SurveyInvoiceFunding.ReadAsync(
                db, order, depositObligation, depositPayments, lockRows: false, cancellationToken);
            if (funding.IsFailure)
                return Result.Failure<ManagerSurveyOrderInvoiceResponse>(funding.Error);

            var openCheckoutIds = await db.Set<SurveyPayment>().AsNoTracking()
                .Where(p => p.SurveyOrderId == order.Id &&
                            p.Purpose == PaymentPurpose.FinalBalance &&
                            (p.Status == SurveyPaymentStatus.Pending ||
                             p.Status == SurveyPaymentStatus.Processing ||
                             p.Status == SurveyPaymentStatus.AdjustmentRequired))
                .OrderBy(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);

            return Result.Success(new ManagerSurveyOrderInvoiceResponse(
                OrderId: order.Id,
                TenantId: order.TenantId,
                FarmId: order.FarmId,
                OrderNumber: order.OrderNumber,
                OrderVersion: order.Version,
                OrderStatus: order.Status.ToString(),
                Scope: order.Scope.ToString(),
                ServiceCode: commercial.ServiceCode,
                ScreensDisease: commercial.ScreensDisease,
                CurrentInvoiceRevisionId: invoice.Id,
                InvoiceRevisionNumber: invoice.RevisionNumber,
                InvoiceFingerprint: invoice.EvidenceFingerprint,
                CurrentApprovedResultRevisionId: approval.Id,
                ApprovedResultRevisionNumber: approval.RevisionNumber,
                ApprovedResultFingerprint: approval.EvidenceFingerprint,
                VerifiedPoleCountSnapshotId: snapshot.Id,
                CountSnapshotRevisionNumber: snapshot.RevisionNumber,
                CountSnapshotFingerprint: snapshot.EvidenceFingerprint,
                SurveyServicePriceId: invoice.SurveyServicePriceId,
                PricePerPole: invoice.PricePerPole,
                Currency: invoice.Currency,
                CommercialPolicyVersionId: invoice.CommercialPolicyVersionId,
                VerifiedBillablePoleCount: invoice.VerifiedBillablePoleCount,
                TotalAmount: invoice.TotalAmount,
                ValidAllocatedDepositNet: invoice.ValidAllocatedDepositNet,
                ConfirmedNetReceipts: funding.Value.ConfirmedNetReceipts,
                DueAmount: invoice.DueAmount,
                OverpaymentAmount: invoice.OverpaymentAmount,
                ZeroPoleApprovedBy: invoice.ZeroPoleApprovedBy,
                ZeroPoleReasonCode: invoice.ZeroPoleReasonCode,
                ZeroPoleEvidenceReference: invoice.ZeroPoleEvidenceReference,
                ComboBatchId: comboBatchId,
                ComboReviewRevisionId: comboReviewRevisionId,
                ComboReviewFingerprint: comboReviewFingerprint,
                OpenFinalBalanceCheckoutPaymentIds: openCheckoutIds,
                FinalizedAt: invoice.FinalizedAt));
        }
        catch (PostgresException exception) when (IsReadConflict(exception))
        {
            return Result.Failure<ManagerSurveyOrderInvoiceResponse>(AppError.Conflict(
                "Invoice.ConcurrencyConflict", "Invoice access changed concurrently. Reload and try again."));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<Result<TenantOwnerSurveyOrderInvoiceResponse>> GetTenantOwnerInvoiceAsync(
        Guid orderId,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
            return Result.Failure<TenantOwnerSurveyOrderInvoiceResponse>(AppError.Validation("OrderId.Required", "OrderId is required."));
        if (actorId == Guid.Empty || tenantId == Guid.Empty)
            return Result.Failure<TenantOwnerSurveyOrderInvoiceResponse>(AppError.Unauthorized("Invoice.Unauthorized", "Valid TenantOwner identity is required."));

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            if (!await HasOwnerAccessAsync(tenantId, actorId, cancellationToken))
                return Result.Failure<TenantOwnerSurveyOrderInvoiceResponse>(AppError.Forbidden("Invoice.OwnerForbidden", "The actor is not an active TenantOwner for this tenant."));

            var order = await (
                from o in db.Set<SurveyOrder>().AsNoTracking()
                join farm in db.Set<Farm>().AsNoTracking()
                    on new { o.TenantId, o.FarmId } equals new { farm.TenantId, FarmId = farm.Id }
                where o.Id == orderId
                      && o.TenantId == tenantId
                      && farm.DeletedAt == null
                      && o.Scope == ContractScope.V3
                      && o.Status != SurveyOrderStatus.Cancelled
                select o)
                .SingleOrDefaultAsync(cancellationToken);

            if (order is null ||
                !order.CurrentInvoiceRevisionId.HasValue ||
                !order.CurrentApprovedResultRevisionId.HasValue ||
                !order.VerifiedPoleCountSnapshotId.HasValue)
            {
                return Result.Failure<TenantOwnerSurveyOrderInvoiceResponse>(
                    AppError.NotFound("Invoice.NotFound", "Survey invoice was not found."));
            }

            var invoice = await db.Set<SurveyInvoiceRevision>().AsNoTracking()
                .SingleOrDefaultAsync(r =>
                    r.Id == order.CurrentInvoiceRevisionId.Value &&
                    r.SurveyOrderId == order.Id &&
                    r.TenantId == order.TenantId &&
                    r.FarmId == order.FarmId &&
                    r.Status == SurveyInvoiceRevision.StatusCurrent,
                    cancellationToken);

            var approvalValid = invoice is not null && await db.Set<SurveyInternalApprovedResultRevision>().AsNoTracking()
                .AnyAsync(a =>
                    a.Id == order.CurrentApprovedResultRevisionId.Value &&
                    a.Id == invoice.ApprovedResultRevisionId &&
                    a.SurveyOrderId == order.Id &&
                    a.TenantId == order.TenantId &&
                    a.FarmId == order.FarmId &&
                    a.IsCurrent &&
                    a.PoleCountSnapshotId == order.VerifiedPoleCountSnapshotId.Value,
                    cancellationToken);

            var snapshotValid = invoice is not null && await db.Set<SurveyOrderPoleCountSnapshot>().AsNoTracking()
                .AnyAsync(s =>
                    s.Id == order.VerifiedPoleCountSnapshotId.Value &&
                    s.Id == invoice.PoleCountSnapshotId &&
                    s.SurveyOrderId == order.Id &&
                    s.TenantId == order.TenantId &&
                    s.FarmId == order.FarmId,
                    cancellationToken);

            if (invoice is null || !approvalValid || !snapshotValid)
            {
                return Result.Failure<TenantOwnerSurveyOrderInvoiceResponse>(
                    AppError.NotFound("Invoice.NotFound", "Survey invoice was not found."));
            }

            await tx.CommitAsync(cancellationToken);

            return Result.Success(new TenantOwnerSurveyOrderInvoiceResponse(
                OrderId: order.Id,
                OrderNumber: order.OrderNumber,
                OrderStatus: order.Status.ToString(),
                InvoiceRevisionNumber: invoice.RevisionNumber,
                VerifiedBillablePoleCount: invoice.VerifiedBillablePoleCount,
                PricePerPole: invoice.PricePerPole,
                Currency: invoice.Currency,
                TotalAmount: invoice.TotalAmount,
                DueAmount: invoice.DueAmount,
                FinalizedAt: invoice.FinalizedAt));
        }
        catch (PostgresException exception) when (IsReadConflict(exception))
        {
            return Result.Failure<TenantOwnerSurveyOrderInvoiceResponse>(AppError.Conflict(
                "Invoice.ConcurrencyConflict", "Invoice access changed concurrently. Reload and try again."));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private static bool IsReadConflict(PostgresException exception) =>
        exception.SqlState is PostgresErrorCodes.SerializationFailure
            or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.LockNotAvailable;

    private async Task<bool> HasOwnerAccessAsync(
        Guid tenantId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var membershipId = await db.Database.SqlQuery<Guid>($"""
            SELECT membership.id AS "Value"
            FROM identity.tenant_memberships membership
            JOIN identity.users app_user ON app_user.id = membership.user_id
            WHERE membership.tenant_id = {tenantId}
              AND membership.user_id = {actorId}
              AND membership.role = 'OWNER'::system.tenant_member_role
              AND membership.status = 'ACTIVE'::system.general_status
              AND app_user.status = 'ACTIVE'::system.user_status
            FOR SHARE OF membership, app_user
            """).FirstOrDefaultAsync(cancellationToken);

        return membershipId != Guid.Empty;
    }
}
