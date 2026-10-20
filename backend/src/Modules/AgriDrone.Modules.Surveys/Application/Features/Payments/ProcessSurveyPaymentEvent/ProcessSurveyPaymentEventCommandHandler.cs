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
            exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Result.Success(Duplicate(command));
        }
        catch (DbUpdateException exception) when (
            exception.HasPostgresSqlState(PostgresErrorCodes.UniqueViolation))
        {
            return Result.Success(Duplicate(command));
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

        var previousPaymentStatus = payment.Status;
        var previousOrderStatus = order.Status;
        var (transitionApplied, disposition) = ApplyEvent(
            command,
            payment,
            order);
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
                SurveyPaymentStatus.Confirmed or
                SurveyPaymentStatus.AdjustmentRequired))
            {
                return (false, "IGNORED_CURRENT_STATE");
            }

            payment.Refund(command.OccurredAt);
            order.RevokePaidServiceReadiness(command.OccurredAt);
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

            if (order.Status != SurveyOrderStatus.AwaitingBaselineDeposit)
            {
                return (false, "ORDER_NOT_AWAITING_BASELINE_DEPOSIT");
            }

            payment.Confirm(command.ProviderReference, command.OccurredAt);
            order.RecordBaselineDepositConfirmed(payment.Id, payment.Amount, command.OccurredAt);
            return (true, "APPLIED");
        }
        else
        {
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
}
