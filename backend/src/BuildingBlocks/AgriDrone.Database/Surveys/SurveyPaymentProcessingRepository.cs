using System.Data.Common;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyPaymentProcessingRepository(
    SurveyPaymentProcessingDbContext context)
    : ISurveyPaymentProcessingRepository
{
    public async Task<Result<IReadOnlyList<SurveyPayment>>>
        GetOwnerPaymentHistoryAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<SurveyPayment>>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await HasOwnerAccessAsync(
                surveyOrderId,
                actorUserId,
                cancellationToken))
        {
            return Result.Failure<IReadOnlyList<SurveyPayment>>(
                PaymentError.Forbidden());
        }

        IReadOnlyList<SurveyPayment> payments =
            await ReadPaymentHistoryAsync(surveyOrderId, cancellationToken);
        return Result.Success(payments);
    }

    public async Task<Result<IReadOnlyList<SurveyPayment>>>
        GetSystemAdminPaymentHistoryAsync(
            Guid surveyOrderId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<SurveyPayment>>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        IReadOnlyList<SurveyPayment> payments =
            await ReadPaymentHistoryAsync(surveyOrderId, cancellationToken);
        return Result.Success(payments);
    }

    private Task<List<SurveyPayment>> ReadPaymentHistoryAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken) => context.SurveyPayments
        .AsNoTracking()
        .Include(payment => payment.Events)
        .Where(payment => payment.SurveyOrderId == surveyOrderId)
        .OrderByDescending(payment => payment.CreatedAt)
        .ToListAsync(cancellationToken);

    public async Task<Result<SurveyPaymentOwnerState>>
        GetForOwnerInitiationAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            PaymentPurpose purpose = PaymentPurpose.FinalBalance,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<SurveyPaymentOwnerState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await HasOwnerAccessAsync(
                surveyOrderId,
                actorUserId,
                cancellationToken))
        {
            return Result.Failure<SurveyPaymentOwnerState>(
                PaymentError.Forbidden());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        var appointmentId = await ReadConfirmedAppointmentForPurposeAsync(
            surveyOrderId,
            purpose,
            evaluatedAt,
            cancellationToken);
        var requiresAppointment =
            (purpose == PaymentPurpose.FinalBalance && !order.PostFlightSettlement) ||
            purpose == PaymentPurpose.OrderDeposit ||
            (order.RequiresBaselineMapping && order.Status == SurveyOrderStatus.AwaitingBaselineDeposit);
        if (!appointmentId.HasValue && requiresAppointment)
        {
            return Result.Failure<SurveyPaymentOwnerState>(
                PaymentError.OrderNotReady());
        }

        var depositObligation = await ReadDepositObligationForUpdateAsync(
            order.DepositObligationId,
            order.Id,
            cancellationToken);

        var purposeString = purpose.ToString();
        var activePayments = await context.SurveyPayments
            .FromSqlInterpolated($$"""
                SELECT payment.*, payment.xmin
                FROM survey.survey_payments AS payment
                WHERE payment.survey_order_id = {{surveyOrderId}}
                  AND payment.purpose = {{purposeString}}
                  AND payment.invoice_revision_id IS NULL
                  AND payment.status IN (
                    'PENDING'::system.survey_payment_status,
                    'PROCESSING'::system.survey_payment_status,
                    'PAID'::system.survey_payment_status,
                    'ADJUSTMENT_REQUIRED'::system.survey_payment_status)
                FOR UPDATE OF payment
                """)
            .ToListAsync(cancellationToken);

        return Result.Success(new SurveyPaymentOwnerState(
            order,
            appointmentId ?? Guid.Empty,
            activePayments.SingleOrDefault(),
            depositObligation));
    }

    public async Task<Result<SurveyPaymentEventState>>
        GetForProviderEventAsync(
            string provider,
            string providerReference,
            string deduplicationKey,
            string? providerTransactionId = null,
            (decimal Amount, string Currency)? receivedMoney = null,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        Guid paymentId;
        Guid orderId;
        // ADR 0020 Rank 2 -> Rank 5: nonlocking lookup first, then lock Order FOR UPDATE before Payment FOR UPDATE.
        await using (var command = CreateCommand(
            """
            SELECT payment.id, payment.survey_order_id
            FROM survey.survey_payments AS payment
            WHERE payment.provider = @provider
              AND payment.provider_reference = @provider_reference
            """,
            Text("provider", provider),
            Text("provider_reference", providerReference)))
        await using (var reader = await command.ExecuteReaderAsync(
            cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Result.Failure<SurveyPaymentEventState>(
                    PaymentError.NotFound(providerReference));
            }

            paymentId = reader.GetGuid(0);
            orderId = reader.GetGuid(1);
        }

        if (!await LockOrderAsync(orderId, cancellationToken))
        {
            return Result.Failure<SurveyPaymentEventState>(
                SurveyOrderError.NotFound(orderId));
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == orderId,
            cancellationToken);
        var depositObligation = await ReadDepositObligationForUpdateAsync(
            order.Scope == ContractScope.V3 ? order.DepositObligationId : null,
            order.Id,
            cancellationToken);

        var lockedPayments = await context.SurveyPayments
            .FromSqlInterpolated($$"""
                SELECT payment.*, payment.xmin
                FROM survey.survey_payments AS payment
                WHERE payment.id = {{paymentId}}
                  AND payment.survey_order_id = {{orderId}}
                  AND payment.provider = {{provider}}
                  AND payment.provider_reference = {{providerReference}}
                FOR UPDATE OF payment
                """)
            .ToListAsync(cancellationToken);

        var payment = lockedPayments.SingleOrDefault();
        if (payment is null)
        {
            return Result.Failure<SurveyPaymentEventState>(
                PaymentError.PersistenceConflict());
        }

        var cancellationDecision = order.Status == SurveyOrderStatus.Cancelled
            ? await ReadCancellationDispositionForUpdateAsync(
                order.Id,
                cancellationToken)
            : null;
        var duplicate = await context.PaymentEvents.AsNoTracking().AnyAsync(
            item => item.Provider == provider &&
                    item.DeduplicationKey == deduplicationKey,
            cancellationToken);

        var normalizedTxId = string.IsNullOrWhiteSpace(providerTransactionId)
            ? null
            : providerTransactionId.Trim();
        var receiptAlreadyRecorded = false;
        if (!duplicate && normalizedTxId is not null)
        {
            // Both callback and Admin transactions check both tables at Serializable isolation.
            await using var checkTransaction = CreateCommand(
                """
                SELECT BOOL_AND(
                    transaction_owner.survey_order_id = @survey_order_id
                    AND transaction_owner.payment_reference = @provider_reference
                    AND (transaction_owner.survey_payment_id IS NULL OR transaction_owner.survey_payment_id = @payment_id)
                    AND (NOT @check_money OR
                         (transaction_owner.amount = @amount AND transaction_owner.currency = @currency)))
                FROM (
                    SELECT receipt.survey_order_id, receipt.payment_reference, receipt.survey_payment_id, receipt.amount, receipt.currency
                    FROM survey.survey_funding_receipts AS receipt
                    WHERE LOWER(receipt.provider) = LOWER(@provider)
                      AND receipt.provider_transaction_id = @provider_transaction_id
                    UNION ALL
                    SELECT decision.survey_order_id, receipt.payment_reference, receipt.survey_payment_id, receipt.amount, receipt.currency
                    FROM survey.survey_funding_reconciliation_decisions AS decision
                    JOIN survey.survey_funding_receipts AS receipt
                      ON receipt.id = decision.funding_receipt_id
                     AND receipt.survey_order_id = decision.survey_order_id
                    WHERE decision.provider_transaction_id = @provider_transaction_id
                      AND decision.disposition_kind IN ('AllocateToCurrentInvoice', 'RouteToRefund')
                      AND LOWER(receipt.provider) = LOWER(@provider)
                ) AS transaction_owner
                """,
                Id("survey_order_id", orderId),
                Id("payment_id", payment.Id),
                Text("provider", provider),
                Text("provider_reference", providerReference),
                Text("provider_transaction_id", normalizedTxId),
                new NpgsqlParameter("check_money", receivedMoney.HasValue),
                new NpgsqlParameter("amount", receivedMoney?.Amount ?? 0m),
                Text("currency", receivedMoney?.Currency.Trim().ToUpperInvariant() ?? string.Empty));

            if (await checkTransaction.ExecuteScalarAsync(cancellationToken) is bool matchesPayment)
            {
                if (!matchesPayment)
                {
                    return Result.Failure<SurveyPaymentEventState>(PaymentError.ReconciliationMismatch());
                }

                receiptAlreadyRecorded = true;
            }
        }

        SurveyBalanceInvoiceContext? currentInvoice = null;
        var priorReceiptsForPaymentAmount = 0m;
        if (!duplicate && !receiptAlreadyRecorded &&
            order.Scope == ContractScope.V3 &&
            payment.Purpose == PaymentPurpose.FinalBalance)
        {
            if (order.CurrentInvoiceRevisionId.HasValue)
            {
                currentInvoice = await ReadBalanceInvoiceContextAsync(
                    order.CurrentInvoiceRevisionId.Value,
                    order.Id,
                    cancellationToken);
            }

            priorReceiptsForPaymentAmount = await ReadPriorReceiptsAmountForPaymentAsync(
                order.Id,
                payment.Id,
                currentInvoice?.InvoiceRevisionId,
                cancellationToken);
        }

        var reversalSource = !duplicate && payment.Purpose == PaymentPurpose.OrderDeposit &&
            depositObligation is not null && payment.DepositObligationId == depositObligation.Id
            ? await ReadDepositReversalSourceAsync(order, payment, depositObligation, cancellationToken)
            : null;

        return Result.Success(new SurveyPaymentEventState(
            order,
            payment,
            duplicate,
            depositObligation,
            cancellationDecision,
            reversalSource,
            receiptAlreadyRecorded,
            currentInvoice,
            priorReceiptsForPaymentAmount));
    }

    private async Task<SurveyBalanceInvoiceContext?> ReadBalanceInvoiceContextAsync(
        Guid invoiceRevisionId,
        Guid surveyOrderId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            """
            SELECT inv.id, inv.currency, inv.total_amount, inv.status
            FROM survey.survey_invoice_revisions AS inv
            WHERE inv.id = @invoice_id
              AND inv.survey_order_id = @order_id
            FOR SHARE OF inv
            """,
            Id("invoice_id", invoiceRevisionId),
            Id("order_id", surveyOrderId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SurveyBalanceInvoiceContext(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetString(3));
    }

    private async Task<decimal> ReadPriorReceiptsAmountForPaymentAsync(
        Guid surveyOrderId,
        Guid surveyPaymentId,
        Guid? currentInvoiceRevisionId,
        CancellationToken cancellationToken)
    {
        if (!currentInvoiceRevisionId.HasValue)
        {
            return 0m;
        }

        // The caller holds the Order lock. Use the effective disposition for each receipt.
        return await context.Database.SqlQuery<decimal>($"""
            SELECT COALESCE(SUM(
                CASE
                    WHEN decision.disposition_kind = 'AllocateToCurrentInvoice'
                         AND decision.target_invoice_revision_id = {currentInvoiceRevisionId.Value}
                         AND (receipt.provider_transaction_id NOT LIKE 'unverified-tx:%'
                              OR decision.provider_transaction_id IS NOT NULL)
                        THEN decision.amount
                    WHEN decision.id IS NULL
                         AND NOT receipt.requires_reconciliation
                         AND receipt.provider_transaction_id NOT LIKE 'unverified-tx:%'
                         AND receipt.invoice_revision_id = {currentInvoiceRevisionId.Value}
                        THEN receipt.amount
                    ELSE 0
                END), 0) AS "Value"
            FROM survey.survey_funding_receipts AS receipt
            LEFT JOIN LATERAL (
                SELECT d.id, d.disposition_kind, d.target_invoice_revision_id, d.provider_transaction_id, d.amount
                FROM survey.survey_funding_reconciliation_decisions AS d
                WHERE d.funding_receipt_id = receipt.id AND d.survey_order_id = receipt.survey_order_id
                ORDER BY (d.disposition_kind <> 'KeepExcluded') DESC, d.reconciled_at DESC, d.id DESC
                LIMIT 1
            ) AS decision ON TRUE
            WHERE receipt.survey_order_id = {surveyOrderId}
              AND receipt.survey_payment_id = {surveyPaymentId}
            """).SingleAsync(cancellationToken);
    }

    private async Task<SurveyDepositReversalSource?> ReadDepositReversalSourceAsync(
        SurveyOrder order,
        SurveyPayment payment,
        SurveyDepositObligation obligation,
        CancellationToken cancellationToken)
    {
        var receipts = await context.Set<SurveyFundingReceipt>()
            .FromSqlInterpolated($$"""
                SELECT receipt.*
                FROM survey.survey_funding_receipts AS receipt
                WHERE receipt.survey_order_id = {{order.Id}}
                  AND receipt.tenant_id = {{order.TenantId}}
                  AND receipt.deposit_obligation_id = {{obligation.Id}}
                  AND receipt.provider = {{payment.Provider}}
                  AND receipt.currency = {{obligation.Currency}}
                  AND receipt.payment_reference = {{payment.ProviderReference}}
                  AND (receipt.survey_payment_id = {{payment.Id}} OR receipt.survey_payment_id IS NULL)
                ORDER BY receipt.received_at, receipt.created_at, receipt.id
                FOR SHARE OF receipt
                """)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (receipts.Count != 1)
            return null;

        var receipt = receipts[0];
        var allocations = await context.Set<SurveyFundingAllocation>()
            .FromSqlInterpolated($$"""
                SELECT allocation.*
                FROM survey.survey_funding_allocations AS allocation
                WHERE allocation.funding_receipt_id = {{receipt.Id}}
                  AND allocation.survey_order_id = {{order.Id}}
                  AND allocation.deposit_obligation_id = {{obligation.Id}}
                  AND allocation.currency = {{receipt.Currency}}
                ORDER BY allocation.id
                FOR SHARE OF allocation
                """)
            .ToListAsync(cancellationToken);
        var allocated = allocations.Sum(a => a.AllocatedAmount);
        if (allocated > receipt.Amount)
            return null;

        var prior = await context.Set<SurveyFundingReversal>()
            .FromSqlInterpolated($$"""
                SELECT reversal.*
                FROM survey.survey_funding_reversals AS reversal
                WHERE reversal.survey_order_id = {{order.Id}}
                  AND reversal.survey_payment_id = {{payment.Id}}
                ORDER BY reversal.id
                FOR SHARE OF reversal
                """)
            .ToListAsync(cancellationToken);
        if (prior.Any(r => r.FundingReceiptId != receipt.Id || r.CashAmount is null ||
            r.DepositObligationId != obligation.Id || r.Currency != obligation.Currency))
            return null;

        return new SurveyDepositReversalSource(
            receipt.Id, receipt.Amount, allocated, prior.Sum(r => r.CashAmount!.Value), prior.Sum(r => r.Amount));
    }

    public async Task<Result<SurveyPriceAdjustmentRequestState>>
        GetForPriceAdjustmentRequestAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<SurveyPriceAdjustmentRequestState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await HasAssignedManagerAccessAsync(
                surveyOrderId,
                actorUserId,
                evaluatedAt,
                cancellationToken))
        {
            return Result.Failure<SurveyPriceAdjustmentRequestState>(
                PriceAdjustmentError.Forbidden());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        var openAdjustments = await context.PriceAdjustments
            .FromSqlInterpolated($$"""
                SELECT adjustment.*, adjustment.xmin
                FROM survey.price_adjustments AS adjustment
                WHERE adjustment.survey_order_id = {{surveyOrderId}}
                  AND adjustment.status IN (
                    'PENDING'::system.price_adjustment_status,
                    'APPROVED'::system.price_adjustment_status)
                FOR UPDATE OF adjustment
                """)
            .ToListAsync(cancellationToken);
        var effectivePrices = await ReadEffectivePricesAsync(
            order.SurveyServiceId,
            evaluatedAt,
            cancellationToken);
        if (effectivePrices.Count != 1)
        {
            return Result.Failure<SurveyPriceAdjustmentRequestState>(
                PriceAdjustmentError.EffectivePriceUnavailable());
        }

        var activePayment = await ReadLatestPaymentAsync(
            surveyOrderId,
            PaymentPurpose.FinalBalance,
            cancellationToken);
        return Result.Success(new SurveyPriceAdjustmentRequestState(
            order,
            effectivePrices[0],
            openAdjustments.SingleOrDefault(),
            activePayment));
    }

    public async Task<Result<SurveyPriceAdjustmentReviewState>>
        GetForPriceAdjustmentReviewAsync(
            Guid surveyOrderId,
            Guid adjustmentId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        if (!await LockOrderAsync(surveyOrderId, cancellationToken))
        {
            return Result.Failure<SurveyPriceAdjustmentReviewState>(
                SurveyOrderError.NotFound(surveyOrderId));
        }

        if (!await ExistsAsync(
                """
                SELECT adjustment.id
                FROM survey.price_adjustments AS adjustment
                WHERE adjustment.id = @adjustment_id
                  AND adjustment.survey_order_id = @order_id
                FOR UPDATE OF adjustment
                """,
                cancellationToken,
                Id("adjustment_id", adjustmentId),
                Id("order_id", surveyOrderId)))
        {
            return Result.Failure<SurveyPriceAdjustmentReviewState>(
                PriceAdjustmentError.NotFound(adjustmentId));
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == surveyOrderId,
            cancellationToken);
        var adjustment = await context.PriceAdjustments.SingleAsync(
            item => item.Id == adjustmentId,
            cancellationToken);
        var activePayment = await ReadLatestPaymentAsync(
            surveyOrderId,
            PaymentPurpose.FinalBalance,
            cancellationToken);
        return Result.Success(new SurveyPriceAdjustmentReviewState(
            order,
            adjustment,
            activePayment));
    }

    public async Task<Result<SurveyPaymentReconciliationState>>
        GetForReconciliationAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default)
    {
        context.EnsureTransaction();
        Guid orderId;
        await using (var command = CreateCommand(
            """
            SELECT payment.survey_order_id
            FROM survey.survey_payments AS payment
            WHERE payment.id = @payment_id
            """,
            Id("payment_id", paymentId)))
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            if (value is not Guid id)
            {
                return Result.Failure<SurveyPaymentReconciliationState>(
                    PaymentError.NotFound(paymentId.ToString("D")));
            }

            orderId = id;
        }

        // ADR 0020 Rank 2 -> Rank 5: Lock Order FOR UPDATE before Payment FOR UPDATE
        if (!await LockOrderAsync(orderId, cancellationToken))
        {
            return Result.Failure<SurveyPaymentReconciliationState>(
                SurveyOrderError.NotFound(orderId));
        }

        var lockedPayments = await context.SurveyPayments
            .FromSqlInterpolated($$"""
                SELECT payment.*, payment.xmin
                FROM survey.survey_payments AS payment
                WHERE payment.id = {{paymentId}}
                  AND payment.survey_order_id = {{orderId}}
                FOR UPDATE OF payment
                """)
            .ToListAsync(cancellationToken);

        var payment = lockedPayments.SingleOrDefault();
        if (payment is null)
        {
            return Result.Failure<SurveyPaymentReconciliationState>(
                PaymentError.PersistenceConflict());
        }

        var order = await context.SurveyOrders.SingleAsync(
            item => item.Id == orderId,
            cancellationToken);
        return Result.Success(new SurveyPaymentReconciliationState(
            order,
            payment));
    }

    public void AddPayment(SurveyPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        context.EnsureTransaction();
        context.SurveyPayments.Add(payment);
    }

    public void AddEvent(PaymentEvent paymentEvent)
    {
        ArgumentNullException.ThrowIfNull(paymentEvent);
        context.EnsureTransaction();
        context.PaymentEvents.Add(paymentEvent);
    }

    public void AddFundingReceipt(SurveyFundingReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        context.EnsureTransaction();
        context.SurveyFundingReceipts.Add(receipt);
    }

    public void AddFundingAllocation(SurveyFundingAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        context.EnsureTransaction();
        context.SurveyFundingAllocations.Add(allocation);
    }

    public void AddFundingReversal(SurveyFundingReversal reversal)
    {
        ArgumentNullException.ThrowIfNull(reversal);
        context.EnsureTransaction();
        context.SurveyFundingReversals.Add(reversal);
    }

    public void AddRefundObligation(SurveyRefundObligation obligation)
    {
        ArgumentNullException.ThrowIfNull(obligation);
        context.EnsureTransaction();
        context.SurveyRefundObligations.Add(obligation);
    }

    public void AddPriceAdjustment(PriceAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        context.EnsureTransaction();
        context.PriceAdjustments.Add(adjustment);
    }

    private Task<bool> LockOrderAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT survey_order.id
            FROM survey.survey_orders AS survey_order
            WHERE survey_order.id = @order_id
            FOR UPDATE OF survey_order
            """,
            cancellationToken,
            Id("order_id", surveyOrderId));

    private Task<bool> HasOwnerAccessAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT membership.id
            FROM survey.survey_orders AS survey_order
            JOIN identity.tenant_memberships AS membership
              ON membership.tenant_id = survey_order.tenant_id
             AND membership.user_id = @actor_id
             AND membership.role = 'OWNER'::system.tenant_member_role
             AND membership.status = 'ACTIVE'::system.general_status
            JOIN identity.users AS app_user
              ON app_user.id = membership.user_id
             AND app_user.status = 'ACTIVE'::system.user_status
            WHERE survey_order.id = @order_id
            FOR SHARE OF membership, app_user
            """,
            cancellationToken,
            Id("order_id", surveyOrderId),
            Id("actor_id", actorUserId));

    private Task<bool> HasAssignedManagerAccessAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken) => ExistsAsync(
            """
            SELECT profile.id
            FROM survey.survey_orders AS survey_order
            JOIN identity.farm_manager_assignments AS assignment
              ON assignment.tenant_id = survey_order.tenant_id
             AND assignment.farm_id = survey_order.farm_id
             AND assignment.ended_at IS NULL
            JOIN identity.system_manager_profiles AS profile
              ON profile.id = assignment.system_manager_profile_id
             AND profile.user_id = @actor_id
             AND profile.status = 'ACTIVE'::system.system_manager_profile_status
            WHERE survey_order.id = @order_id
            FOR SHARE OF assignment, profile
            """,
            cancellationToken,
            Id("order_id", surveyOrderId),
            Id("actor_id", actorUserId));

    private async Task<List<EffectiveSurveyPrice>> ReadEffectivePricesAsync(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        var prices = new List<EffectiveSurveyPrice>();
        await using var command = CreateCommand(
            """
            SELECT price.id, price.price_per_pole, price.currency
            FROM survey.survey_service_prices AS price
            WHERE price.survey_service_id = @service_id
              AND price.price_per_pole IS NOT NULL
              AND price.currency = 'VND'
              AND price.effective_from <= @evaluated_at
              AND (price.effective_to IS NULL OR price.effective_to > @evaluated_at)
            ORDER BY price.effective_from DESC
            LIMIT 2
            FOR SHARE OF price
            """,
            Id("service_id", surveyServiceId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            prices.Add(new EffectiveSurveyPrice(
                reader.GetGuid(0),
                reader.GetDecimal(1),
                reader.GetString(2).Trim()));
        }

        return prices;
    }

    private async Task<SurveyPayment?> ReadLatestPaymentAsync(
        Guid surveyOrderId,
        PaymentPurpose purpose,
        CancellationToken cancellationToken)
    {
        var paymentIds = new List<Guid>();
        await using (var command = CreateCommand(
            """
            SELECT payment.id
            FROM survey.survey_payments AS payment
            WHERE payment.survey_order_id = @order_id
              AND payment.purpose = @purpose
            ORDER BY payment.created_at DESC
            LIMIT 1
            FOR UPDATE OF payment
            """,
            Id("order_id", surveyOrderId),
            Text("purpose", purpose.ToString())))
        await using (var reader = await command.ExecuteReaderAsync(
            cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                paymentIds.Add(reader.GetGuid(0));
            }
        }

        if (paymentIds.Count == 0)
        {
            return null;
        }

        var paymentId = paymentIds[0];
        return await context.SurveyPayments.SingleAsync(
            item => item.Id == paymentId,
            cancellationToken);
    }

    private async Task<Guid?> ReadConfirmedAppointmentForPurposeAsync(
        Guid surveyOrderId,
        PaymentPurpose purpose,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        var purposeSql = purpose switch
        {
            PaymentPurpose.BaselineDeposit =>
                "'BASELINE_MAPPING'::system.survey_appointment_purpose",
            PaymentPurpose.FinalBalance =>
                "'PAID_SERVICE'::system.survey_appointment_purpose",
            PaymentPurpose.OrderDeposit =>
                "'CAPTURE'::system.survey_appointment_purpose",
            _ => throw new InvalidOperationException("Unsupported payment purpose.")
        };

        await using var command = CreateCommand(
            $"""
            SELECT appointment.id
            FROM survey.survey_appointments AS appointment
            WHERE appointment.survey_order_id = @order_id
              AND appointment.purpose = {purposeSql}
              AND appointment.status = 'CONFIRMED'::system.survey_appointment_status
              AND appointment.proposed_end_at > @evaluated_at
            FOR SHARE OF appointment
            """,
            Id("order_id", surveyOrderId),
            new NpgsqlParameter("evaluated_at", evaluatedAt));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private async Task<SurveyDepositObligation?> ReadDepositObligationForUpdateAsync(
        Guid? depositObligationId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        if (!depositObligationId.HasValue)
        {
            return null;
        }

        var obligations = await context.SurveyDepositObligations
            .FromSqlInterpolated($$"""
                SELECT obligation.*
                FROM survey.survey_deposit_obligations AS obligation
                WHERE obligation.id = {{depositObligationId.Value}}
                  AND obligation.survey_order_id = {{orderId}}
                FOR UPDATE OF obligation
                """)
            .ToListAsync(cancellationToken);
        return obligations.SingleOrDefault();
    }

    private async Task<SurveyCancellationReceiptDisposition?>
        ReadCancellationDispositionForUpdateAsync(
            Guid surveyOrderId,
            CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            """
            SELECT decision.id, decision.decision_kind::text
            FROM survey.survey_cancellation_decisions AS decision
            WHERE decision.survey_order_id = @order_id
            ORDER BY decision.created_at DESC
            LIMIT 1
            FOR UPDATE OF decision
            """,
            Id("order_id", surveyOrderId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var decisionKind = reader.GetString(1) switch
        {
            "CUSTOMER_BEFORE_CAPTURE" => CancellationDecisionKind.CustomerBeforeCapture,
            "CUSTOMER_AFTER_ACCEPTABLE_CAPTURE" => CancellationDecisionKind.CustomerAfterAcceptableCapture,
            "OPERATIONAL_POSTPONEMENT" => CancellationDecisionKind.OperationalPostponement,
            "SERVICE_FAILURE" => CancellationDecisionKind.ServiceFailure,
            _ => throw new InvalidOperationException("Unsupported persisted cancellation decision kind.")
        };
        return new SurveyCancellationReceiptDisposition(reader.GetGuid(0), decisionKind);
    }

    private async Task<bool> ExistsAsync(
        string sql,
        CancellationToken cancellationToken,
        params NpgsqlParameter[] parameters)
    {
        await using var command = CreateCommand(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        return await reader.ReadAsync(cancellationToken);
    }

    private DbCommand CreateCommand(
        string sql,
        params NpgsqlParameter[] parameters)
    {
        context.EnsureTransaction();
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction!
            .GetDbTransaction();
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static NpgsqlParameter Id(string name, Guid value) =>
        new(name, value);

    private static NpgsqlParameter Text(string name, string value) =>
        new(name, value);
}
