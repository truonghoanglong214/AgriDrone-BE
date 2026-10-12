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

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;

internal sealed class ReconcileSurveyPaymentCommandHandler(
    ISurveyPaymentProcessingUnitOfWork unitOfWork,
    ISurveyPaymentProcessingRepository repository,
    ISurveyPaymentProcessingOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ReconcileSurveyPaymentCommand,
        Result<ReconcileSurveyPaymentResponse>>
{
    public async Task<Result<ReconcileSurveyPaymentResponse>> Handle(
        ReconcileSurveyPaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ReconcileSurveyPaymentResponse>(
                PaymentError.Forbidden());
        }

        var reconciledAt = timeProvider.GetUtcNow();
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    var stateResult = await repository.GetForReconciliationAsync(
                        command.SurveyPaymentId,
                        token);
                    if (stateResult.IsFailure)
                    {
                        return Result.Failure<ReconcileSurveyPaymentResponse>(
                            stateResult.Error);
                    }

                    var state = stateResult.Value;
                    var order = state.Order;
                    var payment = state.Payment;
                    var normalizedCurrency = command.Currency
                        .Trim()
                        .ToUpperInvariant();
                    if (!string.Equals(
                            payment.ProviderReference,
                            command.ProviderReference.Trim(),
                            StringComparison.Ordinal) ||
                        order.Status != SurveyOrderStatus.AwaitingPayment ||
                        order.FinalPrice != Money.Round(command.SettledAmount) ||
                        !string.Equals(
                            order.Currency,
                            normalizedCurrency,
                            StringComparison.Ordinal))
                    {
                        return Result.Failure<ReconcileSurveyPaymentResponse>(
                            PaymentError.ReconciliationMismatch());
                    }

                    var previousStatus = payment.Status;
                    var previousAmount = payment.Amount;
                    var previousCurrency = payment.Currency;
                    try
                    {
                        order.EnsureVersion(command.ExpectedOrderVersion);
                        payment.EnsureVersion(command.ExpectedPaymentVersion);
                        payment.Reconcile(
                            Money.Create(
                                command.SettledAmount,
                                CurrencyCode.Create(normalizedCurrency)),
                            command.ProviderReference,
                            reconciledAt);
                        order.MarkPaymentConfirmed(reconciledAt);
                    }
                    catch (SurveyDomainException exception)
                    {
                        return Result.Failure<ReconcileSurveyPaymentResponse>(
                            SurveyPaymentCommandSupport.MapDomainError(
                                exception));
                    }

                    using var oldData = JsonSerializer.SerializeToDocument(new
                    {
                        amount = previousAmount,
                        currency = previousCurrency,
                        status = SurveyPaymentCommandSupport.MapStatus(
                            previousStatus)
                    });
                    using var newData = JsonSerializer.SerializeToDocument(new
                    {
                        payment.Amount,
                        payment.Currency,
                        status = SurveyPaymentCommandSupport.MapStatus(
                            payment.Status),
                        command.Reason,
                        command.EvidenceReference,
                        orderStatus = SurveyPaymentCommandSupport.MapOrderStatus(
                            order.Status)
                    });
                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        nameof(SurveyPayment),
                        payment.Id,
                        "RECONCILE_SURVEY_PAYMENT",
                        oldData,
                        newData,
                        reconciledAt);
                    var envelope = IntegrationEventEnvelopeFactory.Create(
                        IntegrationEventDescriptors.SurveyPaymentChangedV1,
                        Guid.CreateVersion7(),
                        executionContext.CorrelationId,
                        order.TenantId,
                        actorId: null,
                        reconciledAt,
                        SurveyPaymentCommandSupport.CreateEvent(
                            order,
                            payment,
                            "PAYMENT_RECONCILED",
                            "APPLIED",
                            reconciledAt));
                    outbox.Add(envelope, order.Id.ToString("D"));
                    await unitOfWork.FlushAsync(token);
                    return Result.Success(new ReconcileSurveyPaymentResponse(
                        payment.Id,
                        order.Id,
                        payment.Provider,
                        payment.ProviderReference!,
                        payment.Amount,
                        payment.Currency,
                        SurveyPaymentCommandSupport.MapStatus(payment.Status),
                        SurveyPaymentCommandSupport.MapOrderStatus(order.Status),
                        payment.Version,
                        order.Version,
                        actorId,
                        reconciledAt));
                },
                cancellationToken);
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

    private static Result<ReconcileSurveyPaymentResponse> Conflict() =>
        Result.Failure<ReconcileSurveyPaymentResponse>(
            PaymentError.PersistenceConflict());
}
