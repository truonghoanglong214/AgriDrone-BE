using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
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

        var previousPaymentStatus = payment.Status;
        var previousOrderStatus = order.Status;
        bool transitionApplied;
        string disposition;
        if (payment.Purpose == PaymentPurpose.OrderDeposit &&
            string.Equals(
                command.EventType.Trim(),
                SurveyPaymentCommandSupport.SucceededEvent,
                StringComparison.OrdinalIgnoreCase))
        {
            var fundingResult = ApplyOrderDepositReceipt(
                command,
                payment,
                order,
                state.DepositObligation,
                state.CancellationDecision,
                repository);
            transitionApplied = fundingResult.Applied;
            disposition = fundingResult.Disposition;
        }
        else if (payment.Purpose == PaymentPurpose.OrderDeposit)
        {
            (transitionApplied, disposition) = ApplyOrderDepositNonReceiptEvent(
                command,
                payment,
                order,
                state.DepositObligation,
                repository);
        }
        else
        {
            (transitionApplied, disposition) = ApplyEvent(
                command,
                payment,
                order);
        }
        var paymentEvent = PaymentEvent.Create(
            payment.Id,
            identity,
            command.EventType,
            command.Payload,
            command.OccurredAt,
            command.ReceivedAt);
        repository.AddEvent(paymentEvent);

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

    private static (bool Applied, string Disposition) ApplyOrderDepositReceipt(
        ProcessSurveyPaymentEventCommand command,
        SurveyPayment payment,
        SurveyOrder order,
        SurveyDepositObligation? obligation,
        SurveyCancellationReceiptDisposition? cancellationDecision,
        ISurveyPaymentProcessingRepository repository)
    {
        if (obligation is null ||
            payment.DepositObligationId != obligation.Id ||
            obligation.SurveyOrderId != order.Id)
        {
            return (false, "DEPOSIT_OBLIGATION_NOT_FOUND");
        }

        if (!string.Equals(command.Currency.Trim(), "VND", StringComparison.Ordinal) ||
            command.Amount <= 0m ||
            decimal.Floor(command.Amount) != command.Amount)
        {
            if (payment.Status is SurveyPaymentStatus.Pending or SurveyPaymentStatus.Processing)
            {
                payment.RequireAdjustment(EffectiveEventTime(command, payment));
                return (true, "AMOUNT_OR_CURRENCY_MISMATCH");
            }

            return (false, "AMOUNT_OR_CURRENCY_MISMATCH");
        }

        var receiptId = Guid.CreateVersion7();
        var payloadBytes = Encoding.UTF8.GetBytes(
            command.Payload.RootElement.GetRawText());
        var payloadFingerprint = Convert.ToHexString(
            SHA256.HashData(payloadBytes));
        var receipt = SurveyFundingReceipt.Create(
            receiptId,
            order.TenantId,
            order.Id,
            obligation.Id,
            payment.Provider,
            command.ProviderEventId,
            command.Amount,
            command.Currency,
            command.ProviderReference,
            payloadFingerprint,
            command.ReceivedAt,
            command.ReceivedAt);
        repository.AddFundingReceipt(receipt);

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
            return (true, "LATE_RECEIPT_AFTER_CANCELLATION");
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

            return (true, "PARTIAL_RECEIPT_ALLOCATED");
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
            : "RECEIPT_UNALLOCATED_OBLIGATION_SATISFIED");
    }

    private static (bool Applied, string Disposition) ApplyOrderDepositNonReceiptEvent(
        ProcessSurveyPaymentEventCommand command,
        SurveyPayment payment,
        SurveyOrder order,
        SurveyDepositObligation? obligation,
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
        if (payment.Status is not (
            SurveyPaymentStatus.Paid or
            SurveyPaymentStatus.AdjustmentRequired))
        {
            return (false, "IGNORED_CURRENT_STATE");
        }
        if (!string.Equals(command.Currency.Trim(), obligation.Currency, StringComparison.Ordinal) ||
            command.Amount <= 0m ||
            decimal.Floor(command.Amount) != command.Amount)
        {
            return (false, "AMOUNT_OR_CURRENCY_MISMATCH");
        }

        var reversedAmount = Math.Min(command.Amount, obligation.AllocatedAmount);
        if (reversedAmount <= 0m)
        {
            return (false, "NO_ALLOCATED_FUNDING_TO_REVERSE");
        }

        var effectiveAt = EffectiveEventTime(command, payment);
        payment.Refund(effectiveAt);
        obligation.ReverseAllocation(reversedAmount, effectiveAt);
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
            command.ReceivedAt));

        if (order.Status is SurveyOrderStatus.ReadyForCapture or
            SurveyOrderStatus.AwaitingReflightDecision)
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
