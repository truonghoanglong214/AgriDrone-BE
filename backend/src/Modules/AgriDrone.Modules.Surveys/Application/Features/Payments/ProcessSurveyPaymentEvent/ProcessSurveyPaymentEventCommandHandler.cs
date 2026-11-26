using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Payments.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;

internal sealed class ProcessSurveyPaymentEventCommandHandler(
    ISurveyPaymentProcessingUnitOfWork unitOfWork,
    ISurveyPaymentProcessingRepository repository,
    ISurveyPaymentProcessingOutbox outbox,
    IExecutionContext executionContext)
    : IRequestHandler<ProcessSurveyPaymentEventCommand,
        Result<ProcessSurveyPaymentEventResponse>>
{
    public async Task<Result<ProcessSurveyPaymentEventResponse>> Handle(
        ProcessSurveyPaymentEventCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<ProcessSurveyPaymentEventResponse>(
                PaymentError.ProviderMismatch());
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => ProcessWithinTransactionAsync(command, token),
                cancellationToken);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            IsDedupConstraint(exception.ConstraintName))
        {
            return Result.Success(Duplicate(command));
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException pgEx &&
            pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
            IsDedupConstraint(pgEx.ConstraintName))
        {
            return Result.Success(Duplicate(command));
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict();
        }
        catch (DbUpdateException exception) when (
            exception.HasPostgresSqlState(PostgresErrorCodes.UniqueViolation))
        {
            return Conflict();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        catch (PostgresException exception) when (exception.SqlState is
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected)
        {
            return Conflict();
        }
        catch (DbUpdateException exception) when (
            exception.HasPostgresSqlState(PostgresErrorCodes.SerializationFailure) ||
            exception.HasPostgresSqlState(PostgresErrorCodes.DeadlockDetected))
        {
            return Conflict();
        }
    }

    private async Task<Result<ProcessSurveyPaymentEventResponse>>
        ProcessWithinTransactionAsync(
            ProcessSurveyPaymentEventCommand command,
            CancellationToken cancellationToken)
    {
        if (!SurveyPaymentCommandSupport.IsSupportedEvent(command.EventType))
        {
            return Result.Failure<ProcessSurveyPaymentEventResponse>(
                PaymentError.UnsupportedEvent(command.EventType));
        }

        var identity = PaymentEventIdentity.Create(
            command.Provider,
            command.ProviderReference,
            command.ProviderEventId);
        var stateResult = await repository.GetForProviderEventAsync(
            identity.Provider,
            identity.ProviderReference,
            identity.DeduplicationKey,
            command.ProviderTransactionId,
            string.Equals(command.EventType.Trim(), SurveyPaymentCommandSupport.SucceededEvent, StringComparison.OrdinalIgnoreCase)
                ? (command.Amount, command.Currency)
                : null,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<ProcessSurveyPaymentEventResponse>(
                stateResult.Error);
        }

        var state = stateResult.Value;
        var payment = state.Payment;
        var order = state.Order;
        if (state.EventAlreadyProcessed)
        {
            return Result.Success(CreateResponse(
                command,
                payment,
                order,
                duplicate: true,
                transitionApplied: false,
                "DUPLICATE"));
        }

        if (!string.Equals(
                payment.Provider,
                identity.Provider,
                StringComparison.Ordinal) ||
            !string.Equals(
                payment.ProviderReference,
                identity.ProviderReference,
                StringComparison.Ordinal))
        {
            return Result.Failure<ProcessSurveyPaymentEventResponse>(
                PaymentError.ProviderMismatch());
        }

        var previousPaymentStatus = payment.Status;
        var previousOrderStatus = order.Status;
        bool transitionApplied;
        string disposition;
        Guid? settlementFundingReceiptId = null;

        if (order.Scope == ContractScope.V3)
        {
            if (payment.Purpose == PaymentPurpose.FinalBalance &&
                payment.Status == SurveyPaymentStatus.Invalidated)
            {
                (transitionApplied, disposition) = ApplyInvalidatedV3BalanceCallback(
                    command,
                    identity,
                    payment,
                    order,
                    state.ReceiptAlreadyRecorded,
                    repository);
            }
            else if (payment.Purpose == PaymentPurpose.FinalBalance)
            {
                var balanceResult = ApplyOpenV3FinalBalanceCallback(
                    command,
                    identity,
                    payment,
                    order,
                    state,
                    repository);
                transitionApplied = balanceResult.Applied;
                disposition = balanceResult.Disposition;
                settlementFundingReceiptId = balanceResult.FundingReceiptId;
            }
            else if (payment.Purpose == PaymentPurpose.OrderDeposit &&
                     string.Equals(
                         command.EventType.Trim(),
                         SurveyPaymentCommandSupport.SucceededEvent,
                         StringComparison.OrdinalIgnoreCase))
            {
                var fundingResult = ApplyOrderDepositReceipt(
                    command,
                    identity,
                    payment,
                    order,
                    state.DepositObligation,
                    state.CancellationDecision,
                    state.ReceiptAlreadyRecorded,
                    repository);
                transitionApplied = fundingResult.Applied;
                disposition = fundingResult.Disposition;
                settlementFundingReceiptId = fundingResult.FundingReceiptId;
            }
            else if (payment.Purpose == PaymentPurpose.OrderDeposit)
            {
                (transitionApplied, disposition) = ApplyOrderDepositNonReceiptEvent(
                    command,
                    payment,
                    order,
                    state.DepositObligation,
                    state.ReversalSource,
                    repository);
                var eventType = command.EventType.Trim().ToLowerInvariant();
                if (transitionApplied && eventType is (SurveyPaymentCommandSupport.RefundedEvent or SurveyPaymentCommandSupport.ChargebackEvent))
                    settlementFundingReceiptId = state.ReversalSource?.FundingReceiptId;
            }
            else
            {
                return Result.Failure<ProcessSurveyPaymentEventResponse>(
                    PaymentError.OrderNotReady());
            }
        }
        else
        {
            if (payment.Status == SurveyPaymentStatus.Paid &&
                payment.Purpose != PaymentPurpose.OrderDeposit &&
                string.Equals(
                    command.EventType.Trim(),
                    SurveyPaymentCommandSupport.SucceededEvent,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(CreateResponse(
                    command,
                    payment,
                    order,
                    duplicate: true,
                    transitionApplied: false,
                    "ALREADY_PAID_IGNORED"));
            }

            (transitionApplied, disposition) = ApplyEvent(
                command,
                payment,
                order);
        }

        if (transitionApplied && previousOrderStatus != SurveyOrderStatus.Completed &&
            order.Status == SurveyOrderStatus.Completed)
        {
            await outbox.AddPostFlightUnlockAsync(order, executionContext.CorrelationId, cancellationToken);
        }
        var paymentEvent = PaymentEvent.Create(
            payment.Id,
            identity,
            command.EventType,
            command.Payload,
            command.OccurredAt,
            command.ReceivedAt);
        repository.AddEvent(paymentEvent);

        if (order.Status == SurveyOrderStatus.AwaitingSettlement &&
            order.CurrentInvoiceRevisionId is Guid currentInvoiceId &&
            settlementFundingReceiptId is Guid fundingReceiptId)
        {
            var trigger = new SurveyReleaseEvaluationRequestedV1(
                order.Id,
                order.TenantId,
                order.FarmId,
                SurveyReleaseTriggerSource.PaymentCallback.ToString(),
                paymentEvent.Id,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(command.Payload.RootElement.GetRawText()))),
                command.ReceivedAt,
                ExpectedApprovedResultRevisionId: order.CurrentApprovedResultRevisionId,
                ExpectedCountSnapshotId: order.VerifiedPoleCountSnapshotId,
                ExpectedInvoiceRevisionId: currentInvoiceId,
                TriggerPaymentId: payment.Id,
                TriggerFundingReceiptId: fundingReceiptId);
            outbox.AddReleaseEvaluationRequested(trigger, executionContext.CorrelationId, null, command.ReceivedAt);
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            paymentStatus = SurveyPaymentCommandSupport.MapStatus(
                previousPaymentStatus),
            orderStatus = previousOrderStatus.ToString()
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            paymentEvent.Id,
            identity.ProviderReference,
            identity.ProviderEventId,
            command.EventType,
            callbackAmount = command.Amount,
            callbackCurrency = command.Currency,
            paymentStatus = SurveyPaymentCommandSupport.MapStatus(
                payment.Status),
            orderStatus = order.Status.ToString(),
            transitionApplied,
            disposition
        });
        unitOfWork.AddAuditLog(AuditLog.ForSystemAction(
            executionContext.CorrelationId,
            nameof(SurveyPayment),
            payment.Id,
            "PROCESS_SURVEY_PAYMENT_EVENT",
            oldData,
            newData,
            command.ReceivedAt));

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyPaymentChangedV1,
            Guid.CreateVersion7(),
            executionContext.CorrelationId,
            order.TenantId,
            actorId: null,
            command.ReceivedAt,
            SurveyPaymentCommandSupport.CreateEvent(
                order,
                payment,
                command.EventType,
                disposition,
                command.ReceivedAt));
        outbox.Add(envelope, order.Id.ToString("D"));
        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(CreateResponse(
            command,
            payment,
            order,
            duplicate: false,
            transitionApplied,
            disposition));
    }

    private static (bool Applied, string Disposition) ApplyEvent(
        ProcessSurveyPaymentEventCommand command,
        SurveyPayment payment,
        SurveyOrder order)
    {
        if (command.OccurredAt < payment.UpdatedAt)
        {
            return (false, "IGNORED_OUT_OF_ORDER");
        }

        var eventType = command.EventType.Trim().ToLowerInvariant();
        if (eventType is SurveyPaymentCommandSupport.RefundedEvent or
            SurveyPaymentCommandSupport.ChargebackEvent)
        {
            if (payment.Status is not (
                SurveyPaymentStatus.Paid or
                SurveyPaymentStatus.AdjustmentRequired))
            {
                return (false, "IGNORED_CURRENT_STATE");
            }

            payment.Refund(command.OccurredAt);
            if (payment.Purpose == PaymentPurpose.BaselineDeposit)
            {
                order.RevokeBaselineDeposit(command.OccurredAt);
            }
            else
            {
                order.RevokePaidServiceReadiness(command.OccurredAt);
            }
            return (true, eventType == SurveyPaymentCommandSupport.ChargebackEvent
                ? "CHARGEBACK_APPLIED"
                : "REFUND_APPLIED");
        }

        if (eventType == SurveyPaymentCommandSupport.ProcessingEvent)
        {
            if (payment.Status != SurveyPaymentStatus.Pending)
            {
                return (false, "IGNORED_CURRENT_STATE");
            }

            payment.StartProcessing(command.ProviderReference, command.OccurredAt);
            return (true, "APPLIED");
        }

        if (eventType == SurveyPaymentCommandSupport.FailedEvent)
        {
            if (payment.Status is not (
                SurveyPaymentStatus.Pending or
                SurveyPaymentStatus.Processing))
            {
                return (false, "IGNORED_CURRENT_STATE");
            }

            payment.Fail(command.OccurredAt);
            return (true, "APPLIED");
        }

        if (payment.Status is not (
            SurveyPaymentStatus.Pending or
            SurveyPaymentStatus.Processing))
        {
            return (false, "IGNORED_CURRENT_STATE");
        }

        if (payment.Purpose == PaymentPurpose.BaselineDeposit)
        {
            if (!order.CanAcceptDepositPayment())
                return (false, "ORDER_NOT_AWAITING_DEPOSIT");
            var amountMatches = payment.Amount == Money.Round(command.Amount) &&
                                order.DepositAmount == payment.Amount;
            var currencyMatches = string.Equals(
                                      payment.Currency,
                                      command.Currency.Trim(),
                                      StringComparison.Ordinal) &&
                                  string.Equals(
                                      command.Currency.Trim(),
                                      "VND",
                                      StringComparison.OrdinalIgnoreCase);
            if (!amountMatches || !currencyMatches)
            {
                payment.RequireAdjustment(command.OccurredAt);
                return (true, "AMOUNT_OR_CURRENCY_MISMATCH");
            }

            if (order.RequiresBaselineMapping)
            {
                payment.Confirm(command.ProviderReference, command.OccurredAt);
                order.RecordBaselineDepositConfirmed(payment.Id, payment.Amount, command.OccurredAt);
                return (true, "APPLIED");
            }
            else
            {
                payment.Confirm(command.ProviderReference, command.OccurredAt);
                order.RecordFollowUpDepositConfirmed(payment.Id, payment.Amount, command.OccurredAt);
                return (true, "APPLIED");
            }
        }
        else
        {
            if (order.PostFlightSettlement && order.ServiceReviewedAt is null)
                return (false, "POST_FLIGHT_REVIEW_REQUIRED");
            if (order.DepositAmount > 0m && order.DepositPaymentStatus != SurveyPaymentStatus.Paid)
            {
                payment.RequireAdjustment(command.OccurredAt);
                return (true, "DEPOSIT_NOT_PAID");
            }
            var expectedBalance = order.RemainingAmount ?? order.FinalPrice;
            var amountMatches = payment.Amount == Money.Round(command.Amount) &&
                                expectedBalance == payment.Amount;
            var currencyMatches = string.Equals(
                                      payment.Currency,
                                      command.Currency.Trim(),
                                      StringComparison.Ordinal) &&
                                  string.Equals(
                                      order.Currency,
                                      payment.Currency,
                                      StringComparison.Ordinal);
            if (!amountMatches || !currencyMatches)
            {
                payment.RequireAdjustment(command.OccurredAt);
                return (true, "AMOUNT_OR_CURRENCY_MISMATCH");
            }

            if (order.Status != SurveyOrderStatus.AwaitingPayment)
            {
                return (false, "ORDER_NOT_AWAITING_PAYMENT");
            }

            payment.Confirm(command.ProviderReference, command.OccurredAt);
            order.MarkPaymentConfirmed(command.OccurredAt);
            return (true, "APPLIED");
        }
    }

    private static (bool Applied, string Disposition, Guid? FundingReceiptId) ApplyOrderDepositReceipt(
        ProcessSurveyPaymentEventCommand command,
        PaymentEventIdentity identity,
        SurveyPayment payment,
        SurveyOrder order,
        SurveyDepositObligation? obligation,
        SurveyCancellationReceiptDisposition? cancellationDecision,
        bool receiptAlreadyRecorded,
        ISurveyPaymentProcessingRepository repository)
    {
        if (obligation is null ||
            payment.DepositObligationId != obligation.Id ||
            obligation.SurveyOrderId != order.Id)
        {
            return (false, "DEPOSIT_OBLIGATION_NOT_FOUND", null);
        }

        if (receiptAlreadyRecorded)
        {
            return (false, "DUPLICATE_PROVIDER_TRANSACTION", null);
        }

        if (!string.Equals(command.Currency.Trim(), "VND", StringComparison.Ordinal) ||
            command.Amount <= 0m ||
            decimal.Floor(command.Amount) != command.Amount)
        {
            if (payment.Status is SurveyPaymentStatus.Pending or SurveyPaymentStatus.Processing)
            {
                payment.RequireAdjustment(EffectiveEventTime(command, payment));
                return (true, "AMOUNT_OR_CURRENCY_MISMATCH", null);
            }

            return (false, "AMOUNT_OR_CURRENCY_MISMATCH", null);
        }

        var receiptId = Guid.CreateVersion7();
        var payloadBytes = Encoding.UTF8.GetBytes(
            command.Payload.RootElement.GetRawText());
        var payloadFingerprint = Convert.ToHexStringLower(
            SHA256.HashData(payloadBytes));
        var normalizedTxId = string.IsNullOrWhiteSpace(command.ProviderTransactionId)
            ? null
            : command.ProviderTransactionId.Trim();
        var missingTxId = normalizedTxId is null;
        var receipt = SurveyFundingReceipt.Create(
            receiptId,
            order.TenantId,
            order.Id,
            obligation.Id,
            payment.Provider,
            normalizedTxId ?? $"unverified-tx:{identity.DeduplicationKey}",
            command.Amount,
            command.Currency,
            command.ProviderReference,
            payloadFingerprint,
            command.ReceivedAt,
            command.ReceivedAt,
            surveyPaymentId: payment.Id,
            requiresReconciliation: missingTxId,
            reconciliationReason: missingTxId ? "MISSING_PROVIDER_TRANSACTION_ID" : null);
        repository.AddFundingReceipt(receipt);

        if (missingTxId)
        {
            if (payment.Status is SurveyPaymentStatus.Pending or SurveyPaymentStatus.Processing or SurveyPaymentStatus.Paid)
            {
                payment.RequireAdjustment(EffectiveEventTime(command, payment));
            }

            return (true, "MISSING_PROVIDER_TRANSACTION_ID_REQUIRES_RECONCILIATION", null);
        }

        if (order.Status == SurveyOrderStatus.Cancelled ||
            obligation.Status == SurveyDepositObligationStatus.Cancelled)
        {
            if (cancellationDecision is not null)
            {
                repository.AddRefundObligation(SurveyRefundObligation.Create(
                    Guid.CreateVersion7(),
                    order.Id,
                    order.TenantId,
                    cancellationDecision.DecisionKind == CancellationDecisionKind.ServiceFailure
                        ? SurveyRefundOriginKind.ServiceFailure
                        : SurveyRefundOriginKind.CancellationReturn,
                    cancellationDecision.DecisionId,
                    command.Amount,
                    command.Currency,
                    command.ReceivedAt,
                    receipt.Id));
            }
            return (true, "LATE_RECEIPT_AFTER_CANCELLATION", null);
        }

        var allocatableAmount = Math.Min(
            command.Amount,
            obligation.RemainingAmount);
        if (allocatableAmount > 0m)
        {
            var allocation = SurveyFundingAllocation.Create(
                Guid.CreateVersion7(),
                receipt.Id,
                order.Id,
                obligation.Id,
                allocatableAmount,
                command.Currency,
                command.ReceivedAt);
            repository.AddFundingAllocation(allocation);
            obligation.ApplyAllocation(allocatableAmount, command.ReceivedAt);
        }

        if (!obligation.IsSatisfied)
        {
            if (payment.Status is SurveyPaymentStatus.Pending or SurveyPaymentStatus.Processing)
            {
                payment.RequireAdjustment(EffectiveEventTime(command, payment));
            }

            return (true, "PARTIAL_RECEIPT_ALLOCATED", receipt.Id);
        }

        if (payment.Status is SurveyPaymentStatus.Pending or
            SurveyPaymentStatus.Processing or
            SurveyPaymentStatus.AdjustmentRequired)
        {
            payment.Confirm(
                command.ProviderReference,
                EffectiveEventTime(command, payment));
        }

        if (order.Status == SurveyOrderStatus.AwaitingDeposit)
        {
            order.TransitionToReadyForCaptureV3(command.ReceivedAt);
        }

        return (true, allocatableAmount > 0m
            ? "DEPOSIT_OBLIGATION_SATISFIED"
            : "RECEIPT_UNALLOCATED_OBLIGATION_SATISFIED", receipt.Id);
    }

    private static (bool Applied, string Disposition) ApplyOrderDepositNonReceiptEvent(
        ProcessSurveyPaymentEventCommand command,
        SurveyPayment payment,
        SurveyOrder order,
        SurveyDepositObligation? obligation,
        SurveyDepositReversalSource? reversalSource,
        ISurveyPaymentProcessingRepository repository)
    {
        if (command.OccurredAt < payment.UpdatedAt)
        {
            return (false, "IGNORED_OUT_OF_ORDER");
        }

        var eventType = command.EventType.Trim().ToLowerInvariant();
        if (eventType == SurveyPaymentCommandSupport.ProcessingEvent &&
            payment.Status == SurveyPaymentStatus.Pending)
        {
            payment.StartProcessing(command.ProviderReference, command.OccurredAt);
            return (true, "APPLIED");
        }

        if (eventType == SurveyPaymentCommandSupport.FailedEvent &&
            payment.Status is SurveyPaymentStatus.Pending or SurveyPaymentStatus.Processing)
        {
            payment.Fail(command.OccurredAt);
            return (true, "APPLIED");
        }

        if (eventType is not (SurveyPaymentCommandSupport.RefundedEvent or
            SurveyPaymentCommandSupport.ChargebackEvent))
        {
            return (false, "IGNORED_CURRENT_STATE");
        }
        if (obligation is null ||
            payment.DepositObligationId != obligation.Id ||
            obligation.SurveyOrderId != order.Id)
        {
            return (false, "DEPOSIT_OBLIGATION_NOT_FOUND");
        }
        if (!string.Equals(command.Currency.Trim(), obligation.Currency, StringComparison.Ordinal) ||
            command.Amount <= 0m ||
            decimal.Floor(command.Amount) != command.Amount)
        {
            return (false, "AMOUNT_OR_CURRENCY_MISMATCH");
        }

        var source = reversalSource;
        var reversedAmount = 0m;
        Guid? receiptId = null;
        if (source is not null && command.Amount <= source.ReceivedAmount - source.PriorCashReversed)
        {
            var cumulativeCash = source.PriorCashReversed + command.Amount;
            var unallocatedCash = source.ReceivedAmount - source.AllocatedAmount;
            var cumulativeAllocationReversed = Math.Max(0m, cumulativeCash - unallocatedCash);
            var allocationDelta = cumulativeAllocationReversed - source.PriorAllocationReversed;
            if (allocationDelta >= 0m && allocationDelta <= obligation.AllocatedAmount)
            {
                reversedAmount = allocationDelta;
                receiptId = source.FundingReceiptId;
            }
        }

        repository.AddFundingReversal(SurveyFundingReversal.Create(
            Guid.CreateVersion7(),
            payment.Id,
            order.Id,
            obligation.Id,
            payment.Provider,
            command.ProviderEventId,
            command.EventType,
            reversedAmount,
            obligation.Currency,
            command.OccurredAt,
            command.ReceivedAt >= command.OccurredAt ? command.ReceivedAt : command.OccurredAt,
            receiptId,
            command.Amount));

        if (!receiptId.HasValue)
            return (false, "FUNDING_REVERSAL_REQUIRES_RECONCILIATION");

        var effectiveAt = EffectiveEventTime(command, payment);
        if (source!.PriorCashReversed + command.Amount == source.ReceivedAmount &&
            payment.Status is (SurveyPaymentStatus.Paid or SurveyPaymentStatus.AdjustmentRequired))
        {
            payment.Refund(effectiveAt);
        }

        if (reversedAmount == 0m)
            return (true, "UNALLOCATED_FUNDING_REVERSAL_RECORDED");

        obligation.ReverseAllocation(reversedAmount, effectiveAt);

        if (!obligation.IsSatisfied && order.Status is (SurveyOrderStatus.ReadyForCapture or
            SurveyOrderStatus.AwaitingReflightDecision))
        {
            order.TransitionToAwaitingDepositV3(effectiveAt);
            return (true, eventType == SurveyPaymentCommandSupport.ChargebackEvent
                ? "CHARGEBACK_REVERSED_CAPTURE_READINESS"
                : "REFUND_REVERSED_CAPTURE_READINESS");
        }

        return (true, order.Status == SurveyOrderStatus.Capturing
            ? "FUNDING_REVERSAL_RECORDED_AFTER_CAPTURE_START"
            : "FUNDING_REVERSAL_RECORDED");
    }

    private static (bool Applied, string Disposition) ApplyInvalidatedV3BalanceCallback(
        ProcessSurveyPaymentEventCommand command,
        PaymentEventIdentity identity,
        SurveyPayment payment,
        SurveyOrder order,
        bool receiptAlreadyRecorded,
        ISurveyPaymentProcessingRepository repository)
    {
        var eventType = command.EventType.Trim().ToLowerInvariant();
        if (eventType is SurveyPaymentCommandSupport.RefundedEvent or SurveyPaymentCommandSupport.ChargebackEvent)
        {
            return (false, "INVALIDATED_CHECKOUT_REVERSAL_REQUIRES_RECONCILIATION");
        }

        if (eventType != SurveyPaymentCommandSupport.SucceededEvent)
        {
            return (false, "IGNORED_INVALIDATED_CHECKOUT");
        }

        if (receiptAlreadyRecorded)
        {
            return (false, "DUPLICATE_PROVIDER_TRANSACTION");
        }

        if (!string.Equals(command.Currency.Trim(), "VND", StringComparison.Ordinal) ||
            command.Amount <= 0m ||
            decimal.Floor(command.Amount) != command.Amount)
        {
            return (false, "AMOUNT_OR_CURRENCY_MISMATCH");
        }

        var payloadBytes = Encoding.UTF8.GetBytes(
            command.Payload.RootElement.GetRawText());
        var payloadFingerprint = Convert.ToHexStringLower(
            SHA256.HashData(payloadBytes));

        var normalizedTxId = string.IsNullOrWhiteSpace(command.ProviderTransactionId)
            ? null
            : command.ProviderTransactionId.Trim();
        var reconciliationReason = normalizedTxId is null
            ? "MISSING_PROVIDER_TRANSACTION_ID_ON_INVALIDATED_CHECKOUT"
            : payment.InvoiceRevisionId.HasValue
                ? "LATE_RECEIPT_ON_INVALIDATED_CHECKOUT"
                : "LATE_RECEIPT_ON_INVALIDATED_CHECKOUT_MISSING_INVOICE_LINKAGE";

        var receipt = SurveyFundingReceipt.Create(
            id: Guid.CreateVersion7(),
            tenantId: order.TenantId,
            surveyOrderId: order.Id,
            depositObligationId: null,
            provider: payment.Provider,
            providerTransactionId: normalizedTxId ?? $"unverified-tx:{identity.DeduplicationKey}",
            amount: command.Amount,
            currency: command.Currency,
            paymentReference: command.ProviderReference,
            payloadFingerprint: payloadFingerprint,
            receivedAt: command.ReceivedAt,
            createdAt: command.ReceivedAt,
            surveyPaymentId: payment.Id,
            invoiceRevisionId: payment.InvoiceRevisionId,
            requiresReconciliation: true,
            reconciliationReason: reconciliationReason);

        repository.AddFundingReceipt(receipt);

        // Preserve payment.Status == Invalidated and never auto-credit the replacement invoice.
        return (true, normalizedTxId is null
            ? "MISSING_PROVIDER_TRANSACTION_ID_REQUIRES_RECONCILIATION"
            : payment.InvoiceRevisionId.HasValue
                ? "LATE_RECEIPT_RECORDED_FOR_RECONCILIATION"
                : "LATE_RECEIPT_MISSING_INVOICE_LINKAGE_RECONCILIATION");
    }

    private static (bool Applied, string Disposition, Guid? FundingReceiptId) ApplyOpenV3FinalBalanceCallback(
        ProcessSurveyPaymentEventCommand command,
        PaymentEventIdentity identity,
        SurveyPayment payment,
        SurveyOrder order,
        SurveyPaymentEventState state,
        ISurveyPaymentProcessingRepository repository)
    {
        var eventType = command.EventType.Trim().ToLowerInvariant();
        var effectiveAt = EffectiveEventTime(command, payment);

        if (eventType is SurveyPaymentCommandSupport.RefundedEvent or SurveyPaymentCommandSupport.ChargebackEvent)
        {
            if (payment.Status is SurveyPaymentStatus.Pending or
                SurveyPaymentStatus.Processing or
                SurveyPaymentStatus.Paid)
            {
                payment.RequireAdjustment(effectiveAt);
                return (true, "BALANCE_REVERSAL_REQUIRES_RECONCILIATION", null);
            }

            return (false, "BALANCE_REVERSAL_REQUIRES_RECONCILIATION", null);
        }

        if (eventType == SurveyPaymentCommandSupport.ProcessingEvent)
        {
            if (command.OccurredAt < payment.UpdatedAt ||
                payment.Status != SurveyPaymentStatus.Pending)
            {
                return (false, "IGNORED_CURRENT_STATE", null);
            }

            payment.StartProcessing(command.ProviderReference, effectiveAt);
            return (true, "PAYMENT_PROCESSING", null);
        }

        if (eventType == SurveyPaymentCommandSupport.FailedEvent)
        {
            if (command.OccurredAt < payment.UpdatedAt ||
                payment.Status is not (SurveyPaymentStatus.Pending or SurveyPaymentStatus.Processing))
            {
                return (false, "IGNORED_CURRENT_STATE", null);
            }

            payment.Fail(effectiveAt);
            return (true, "PAYMENT_FAILED", null);
        }

        if (eventType != SurveyPaymentCommandSupport.SucceededEvent)
        {
            return (false, "IGNORED_UNSUPPORTED_EVENT", null);
        }

        if (state.ReceiptAlreadyRecorded)
        {
            return (false, "DUPLICATE_PROVIDER_TRANSACTION", null);
        }

        var normalizedCurrency = command.Currency.Trim().ToUpperInvariant();
        if (!string.Equals(normalizedCurrency, "VND", StringComparison.Ordinal) ||
            !string.Equals(payment.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase) ||
            command.Amount <= 0m ||
            decimal.Floor(command.Amount) != command.Amount)
        {
            if (payment.Status is SurveyPaymentStatus.Pending or
                SurveyPaymentStatus.Processing or
                SurveyPaymentStatus.Paid)
            {
                payment.RequireAdjustment(effectiveAt);
                return (true, "AMOUNT_OR_CURRENCY_MISMATCH", null);
            }

            return (false, "AMOUNT_OR_CURRENCY_MISMATCH", null);
        }

        var payloadBytes = Encoding.UTF8.GetBytes(
            command.Payload.RootElement.GetRawText());
        var payloadFingerprint = Convert.ToHexStringLower(
            SHA256.HashData(payloadBytes));

        var normalizedTxId = string.IsNullOrWhiteSpace(command.ProviderTransactionId)
            ? null
            : command.ProviderTransactionId.Trim();

        if (normalizedTxId is null)
        {
            var missingTxReceipt = SurveyFundingReceipt.Create(
                id: Guid.CreateVersion7(),
                tenantId: order.TenantId,
                surveyOrderId: order.Id,
                depositObligationId: null,
                provider: payment.Provider,
                providerTransactionId: $"unverified-tx:{identity.DeduplicationKey}",
                amount: command.Amount,
                currency: normalizedCurrency,
                paymentReference: command.ProviderReference,
                payloadFingerprint: payloadFingerprint,
                receivedAt: command.ReceivedAt,
                createdAt: command.ReceivedAt,
                surveyPaymentId: payment.Id,
                invoiceRevisionId: payment.InvoiceRevisionId,
                requiresReconciliation: true,
                reconciliationReason: "MISSING_PROVIDER_TRANSACTION_ID");
            repository.AddFundingReceipt(missingTxReceipt);

            if (payment.Status is SurveyPaymentStatus.Pending or
                SurveyPaymentStatus.Processing)
            {
                payment.RequireAdjustment(effectiveAt);
            }

            return (true, "MISSING_PROVIDER_TRANSACTION_ID_REQUIRES_RECONCILIATION", null);
        }

        var currentInvoice = state.CurrentInvoice;
        string? holdReason = null;
        if (order.Status == SurveyOrderStatus.Cancelled)
        {
            holdReason = "LATE_RECEIPT_AFTER_ORDER_CANCELLATION";
        }
        else if (order.Status == SurveyOrderStatus.Completed)
        {
            holdReason = "EXTRA_RECEIPT_AFTER_ORDER_COMPLETION";
        }
        else if (order.Status != SurveyOrderStatus.AwaitingSettlement)
        {
            holdReason = "RECEIPT_OUTSIDE_AWAITING_SETTLEMENT";
        }
        else if (!payment.InvoiceRevisionId.HasValue ||
                 currentInvoice is null ||
                 currentInvoice.Status != SurveyInvoiceRevision.StatusCurrent ||
                 payment.InvoiceRevisionId.Value != currentInvoice.InvoiceRevisionId)
        {
            holdReason = "INVOICE_REVISION_MISMATCH_OR_MISSING_LINKAGE";
        }
        else if (!string.Equals(currentInvoice.Currency, normalizedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            holdReason = "INVOICE_CURRENCY_MISMATCH";
        }

        if (holdReason is not null)
        {
            var holdReceipt = SurveyFundingReceipt.Create(
                id: Guid.CreateVersion7(),
                tenantId: order.TenantId,
                surveyOrderId: order.Id,
                depositObligationId: null,
                provider: payment.Provider,
                providerTransactionId: normalizedTxId,
                amount: command.Amount,
                currency: normalizedCurrency,
                paymentReference: command.ProviderReference,
                payloadFingerprint: payloadFingerprint,
                receivedAt: command.ReceivedAt,
                createdAt: command.ReceivedAt,
                surveyPaymentId: payment.Id,
                invoiceRevisionId: payment.InvoiceRevisionId,
                requiresReconciliation: true,
                reconciliationReason: holdReason);
            repository.AddFundingReceipt(holdReceipt);

            if (payment.Status is SurveyPaymentStatus.Pending or
                SurveyPaymentStatus.Processing)
            {
                payment.RequireAdjustment(effectiveAt);
            }

            return (true, $"{holdReason}_RECONCILIATION", null);
        }

        var cumulativeAmount = state.PriorReceiptsForPaymentAmount + command.Amount;
        if (cumulativeAmount > payment.Amount)
        {
            var overIntentReceipt = SurveyFundingReceipt.Create(
                id: Guid.CreateVersion7(),
                tenantId: order.TenantId,
                surveyOrderId: order.Id,
                depositObligationId: null,
                provider: payment.Provider,
                providerTransactionId: normalizedTxId,
                amount: command.Amount,
                currency: normalizedCurrency,
                paymentReference: command.ProviderReference,
                payloadFingerprint: payloadFingerprint,
                receivedAt: command.ReceivedAt,
                createdAt: command.ReceivedAt,
                surveyPaymentId: payment.Id,
                invoiceRevisionId: payment.InvoiceRevisionId,
                requiresReconciliation: true,
                reconciliationReason: "RECEIPT_EXCEEDS_CHECKOUT_INTENT_AMOUNT");
            repository.AddFundingReceipt(overIntentReceipt);

            if (payment.Status is SurveyPaymentStatus.Pending or
                SurveyPaymentStatus.Processing)
            {
                payment.RequireAdjustment(effectiveAt);
            }

            return (true, "BALANCE_RECEIPT_EXCEEDS_INTENT_RECONCILIATION", null);
        }

        var validReceipt = SurveyFundingReceipt.Create(
            id: Guid.CreateVersion7(),
            tenantId: order.TenantId,
            surveyOrderId: order.Id,
            depositObligationId: null,
            provider: payment.Provider,
            providerTransactionId: normalizedTxId,
            amount: command.Amount,
            currency: normalizedCurrency,
            paymentReference: command.ProviderReference,
            payloadFingerprint: payloadFingerprint,
            receivedAt: command.ReceivedAt,
            createdAt: command.ReceivedAt,
            surveyPaymentId: payment.Id,
            invoiceRevisionId: payment.InvoiceRevisionId,
            requiresReconciliation: false,
            reconciliationReason: null);
        repository.AddFundingReceipt(validReceipt);

        if (cumulativeAmount == payment.Amount)
        {
            if (payment.Status is SurveyPaymentStatus.Pending or
                SurveyPaymentStatus.Processing or
                SurveyPaymentStatus.AdjustmentRequired)
            {
                payment.Confirm(command.ProviderReference, effectiveAt);
            }

            return (true, "FINAL_BALANCE_RECEIPT_CONFIRMED", validReceipt.Id);
        }

        if (payment.Status == SurveyPaymentStatus.Pending)
        {
            payment.StartProcessing(command.ProviderReference, effectiveAt);
        }

        return (true, "PARTIAL_FINAL_BALANCE_RECEIPT_RECORDED", validReceipt.Id);
    }

    private static DateTimeOffset EffectiveEventTime(
        ProcessSurveyPaymentEventCommand command,
        SurveyPayment payment) => new[]
        {
            command.OccurredAt,
            command.ReceivedAt,
            payment.UpdatedAt
        }.Max();

    private static ProcessSurveyPaymentEventResponse CreateResponse(
        ProcessSurveyPaymentEventCommand command,
        SurveyPayment payment,
        SurveyOrder order,
        bool duplicate,
        bool transitionApplied,
        string disposition) => new(
            payment.Id,
            order.Id,
            command.Provider,
            command.ProviderReference,
            command.ProviderEventId,
            command.EventType,
            SurveyPaymentCommandSupport.MapStatus(payment.Status),
            SurveyPaymentCommandSupport.MapOrderStatus(order.Status),
            duplicate,
            transitionApplied,
            disposition,
            command.ReceivedAt);

    private static ProcessSurveyPaymentEventResponse Duplicate(
        ProcessSurveyPaymentEventCommand command) => new(
            null,
            null,
            command.Provider,
            command.ProviderReference,
            command.ProviderEventId,
            command.EventType,
            null,
            null,
            true,
            false,
            "DUPLICATE_CONCURRENT",
            command.ReceivedAt);

    private static Result<ProcessSurveyPaymentEventResponse> Conflict() =>
        Result.Failure<ProcessSurveyPaymentEventResponse>(
            PaymentError.PersistenceConflict());

    private static bool IsDedupConstraint(string? constraintName) =>
        constraintName is "uq_payment_events_provider_dedup" or
                          "uq_payment_events_provider_reference_event" or
                          "uq_survey_funding_receipts_provider_transaction";
}
