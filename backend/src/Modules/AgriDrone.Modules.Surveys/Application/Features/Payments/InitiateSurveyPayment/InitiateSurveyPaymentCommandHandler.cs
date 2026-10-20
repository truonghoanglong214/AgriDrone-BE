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

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;

internal sealed class InitiateSurveyPaymentCommandHandler(
    ISurveyPaymentProcessingUnitOfWork unitOfWork,
    ISurveyPaymentProcessingRepository repository,
    ISurveyPaymentProcessingOutbox outbox,
    ISurveyPaymentGateway gateway,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<InitiateSurveyPaymentCommand,
        Result<InitiateSurveyPaymentResponse>>
{
    public async Task<Result<InitiateSurveyPaymentResponse>> Handle(
        InitiateSurveyPaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<InitiateSurveyPaymentResponse>(
                PaymentError.Forbidden());
        }

        var initiatedAt = timeProvider.GetUtcNow();
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => InitiateWithinTransactionAsync(
                    command,
                    actorId,
                    initiatedAt,
                    token),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        catch (PostgresException exception) when (IsConflict(exception.SqlState))
        {
            return Conflict();
        }
        catch (DbUpdateException exception) when (
            exception.HasPostgresSqlState(PostgresErrorCodes.UniqueViolation) ||
            exception.HasPostgresSqlState(PostgresErrorCodes.SerializationFailure) ||
            exception.HasPostgresSqlState(PostgresErrorCodes.DeadlockDetected))
        {
            return Conflict();
        }
    }

    private async Task<Result<InitiateSurveyPaymentResponse>>
        InitiateWithinTransactionAsync(
            InitiateSurveyPaymentCommand command,
            Guid actorId,
            DateTimeOffset initiatedAt,
            CancellationToken cancellationToken)
    {
        var stateResult = await repository.GetForOwnerInitiationAsync(
            command.SurveyOrderId,
            actorId,
            initiatedAt,
            command.Purpose,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<InitiateSurveyPaymentResponse>(
                stateResult.Error);
        }

        var state = stateResult.Value;
        var order = state.Order;
        try
        {
            order.EnsureVersion(command.ExpectedOrderVersion);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<InitiateSurveyPaymentResponse>(
                SurveyPaymentCommandSupport.MapDomainError(exception));
        }

        Money amount;
        if (command.Purpose == PaymentPurpose.BaselineDeposit)
        {
            if (order.Status != SurveyOrderStatus.AwaitingBaselineDeposit ||
                order.DepositPaymentStatus == SurveyPaymentStatus.Confirmed ||
                order.DepositAmount <= 0)
            {
                return Result.Failure<InitiateSurveyPaymentResponse>(
                    PaymentError.OrderNotReady());
            }

            amount = Money.Create(order.DepositAmount, CurrencyCode.Vnd);
        }
        else
        {
            if (order.Status != SurveyOrderStatus.AwaitingPayment ||
                order.FinalPrice is not > 0 ||
                string.IsNullOrWhiteSpace(order.Currency) ||
                order.PricePerPoleSnapshot is null ||
                order.ConfirmedSurveyPoleCount is null)
            {
                return Result.Failure<InitiateSurveyPaymentResponse>(
                    PaymentError.OrderNotReady());
            }

            var payableAmount = order.RemainingAmount ?? order.FinalPrice.Value;
            if (payableAmount == 0)
            {
                order.MarkPaymentConfirmed(initiatedAt);

                using var zeroData = JsonSerializer.SerializeToDocument(new
                {
                    orderId = order.Id,
                    remainingAmount = 0m,
                    provider = "SETTLEMENT",
                    providerReference = "ZERO_BALANCE_SETTLED",
                    status = "CONFIRMED"
                });
                auditWriter.AddUserAction(
                    unitOfWork,
                    order.TenantId,
                    order.FarmId,
                    actorId,
                    executionContext.CorrelationId,
                    nameof(SurveyOrder),
                    order.Id,
                    "SETTLE_ZERO_BALANCE_SURVEY_PAYMENT",
                    null,
                    zeroData,
                    initiatedAt);

                await unitOfWork.FlushAsync(cancellationToken);

                return Result.Success(new InitiateSurveyPaymentResponse(
                    Guid.Empty,
                    order.Id,
                    state.PaidServiceAppointmentId,
                    0m,
                    order.Currency ?? "VND",
                    "SETTLEMENT",
                    "ZERO_BALANCE_SETTLED",
                    "CONFIRMED",
                    new Uri("about:blank"),
                    string.Empty,
                    initiatedAt,
                    0,
                    order.Version,
                    initiatedAt,
                    state.PaidServiceAppointmentId));
            }

            amount = Money.Create(
                payableAmount,
                CurrencyCode.Create(order.Currency));
        }

        if (state.ActivePayment is not null)
        {
            return Result.Failure<InitiateSurveyPaymentResponse>(
                PaymentError.ActivePaymentExists());
        }

        var payment = SurveyPayment.Create(
            order.Id,
            amount,
            gateway.Provider,
            initiatedAt,
            command.Purpose);
        var sessionResult = await gateway.CreateSessionAsync(
            new CreatePaymentSession(
                payment.Id,
                order.Id,
                order.OrderNumber,
                payment.Amount,
                payment.Currency,
                initiatedAt),
            cancellationToken);
        if (sessionResult.IsFailure)
        {
            return Result.Failure<InitiateSurveyPaymentResponse>(
                sessionResult.Error);
        }

        var session = sessionResult.Value;
        payment.StartProcessing(session.ProviderReference, initiatedAt);
        repository.AddPayment(payment);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            payment.Id,
            payment.SurveyOrderId,
            payment.Amount,
            payment.Currency,
            payment.Provider,
            payment.ProviderReference,
            paymentStatus = SurveyPaymentCommandSupport.MapStatus(
                payment.Status),
            state.PaidServiceAppointmentId,
            session.ExpiresAt
        });
        auditWriter.AddUserAction(
            unitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(SurveyPayment),
            payment.Id,
            "INITIATE_SURVEY_PAYMENT",
            null,
            newData,
            initiatedAt);

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyPaymentChangedV1,
            Guid.CreateVersion7(),
            executionContext.CorrelationId,
            order.TenantId,
            actorId,
            initiatedAt,
            SurveyPaymentCommandSupport.CreateEvent(
                order,
                payment,
                "PAYMENT_INITIATED",
                "APPLIED",
                initiatedAt));
        outbox.Add(envelope, order.Id.ToString("D"));
        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(new InitiateSurveyPaymentResponse(
            payment.Id,
            order.Id,
            state.PaidServiceAppointmentId,
            payment.Amount,
            payment.Currency,
            payment.Provider,
            payment.ProviderReference!,
            SurveyPaymentCommandSupport.MapStatus(payment.Status),
            session.RedirectUrl,
            session.ClientToken,
            session.ExpiresAt,
            payment.Version,
            order.Version,
            initiatedAt,
            state.PaidServiceAppointmentId));
    }

    private static bool IsConflict(string? sqlState) => sqlState is
        PostgresErrorCodes.UniqueViolation or
        PostgresErrorCodes.SerializationFailure or
        PostgresErrorCodes.DeadlockDetected;

    private static Result<InitiateSurveyPaymentResponse> Conflict() =>
        Result.Failure<InitiateSurveyPaymentResponse>(
            PaymentError.PersistenceConflict());
}
