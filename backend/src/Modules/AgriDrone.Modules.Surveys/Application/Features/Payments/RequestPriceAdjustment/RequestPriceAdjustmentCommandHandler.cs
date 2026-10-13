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

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.RequestPriceAdjustment;

internal sealed class RequestPriceAdjustmentCommandHandler(
    ISurveyPaymentProcessingUnitOfWork unitOfWork,
    ISurveyPaymentProcessingRepository repository,
    ISurveyPaymentProcessingOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RequestPriceAdjustmentCommand,
        Result<RequestPriceAdjustmentResponse>>
{
    public async Task<Result<RequestPriceAdjustmentResponse>> Handle(
        RequestPriceAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<RequestPriceAdjustmentResponse>(
                PriceAdjustmentError.Forbidden());
        }

        var requestedAt = timeProvider.GetUtcNow();
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => RequestWithinTransactionAsync(
                    command,
                    actorId,
                    requestedAt,
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

    private async Task<Result<RequestPriceAdjustmentResponse>>
        RequestWithinTransactionAsync(
            RequestPriceAdjustmentCommand command,
            Guid actorId,
            DateTimeOffset requestedAt,
            CancellationToken cancellationToken)
    {
        var stateResult = await repository.GetForPriceAdjustmentRequestAsync(
            command.SurveyOrderId,
            actorId,
            requestedAt,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<RequestPriceAdjustmentResponse>(
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
            return Result.Failure<RequestPriceAdjustmentResponse>(
                PriceAdjustmentCommandSupport.MapDomainError(exception));
        }

        if (state.OpenAdjustment is not null)
        {
            return Result.Failure<RequestPriceAdjustmentResponse>(
                PriceAdjustmentError.PendingAlreadyExists());
        }

        if (order.Status is not (
                SurveyOrderStatus.AwaitingPaidAppointment or
                SurveyOrderStatus.AwaitingPayment or
                SurveyOrderStatus.ReadyForPaidService) ||
            order.ConfirmedSurveyPoleCount is null ||
            order.SurveyServicePriceId is null ||
            order.PricePerPoleSnapshot is null ||
            order.FinalPrice is null ||
            string.IsNullOrWhiteSpace(order.Currency) ||
            state.ActivePayment?.Status is SurveyPaymentStatus.Pending or
                SurveyPaymentStatus.Processing)
        {
            return Result.Failure<RequestPriceAdjustmentResponse>(
                PriceAdjustmentError.OrderNotEligible());
        }

        PriceAdjustment adjustment;
        try
        {
            adjustment = PriceAdjustment.Create(
                order.Id,
                order.ConfirmedSurveyPoleCount.Value,
                ConfirmedSurveyPoleCount.Create(command.NewConfirmedPoleCount),
                order.SurveyServicePriceId.Value,
                state.EffectivePrice.SurveyServicePriceId,
                order.PricePerPoleSnapshot.Value,
                PricePerPole.Create(state.EffectivePrice.PricePerPole),
                CurrencyCode.Create(state.EffectivePrice.Currency),
                command.Reason,
                command.EvidenceReference,
                actorId,
                requestedAt);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<RequestPriceAdjustmentResponse>(
                PriceAdjustmentCommandSupport.MapDomainError(exception));
        }

        repository.AddPriceAdjustment(adjustment);
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            adjustment.Id,
            adjustment.SurveyOrderId,
            adjustment.OldPoleCount,
            adjustment.NewPoleCount,
            adjustment.OldSurveyServicePriceId,
            adjustment.NewSurveyServicePriceId,
            adjustment.OldPricePerPole,
            adjustment.NewPricePerPole,
            adjustment.OldFinalPrice,
            adjustment.NewFinalPrice,
            adjustment.Currency,
            adjustment.Reason,
            adjustment.EvidenceReference
        });
        auditWriter.AddUserAction(
            unitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(PriceAdjustment),
            adjustment.Id,
            "REQUEST_PRICE_ADJUSTMENT",
            null,
            newData,
            requestedAt);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyPriceAdjustmentChangedV1,
            Guid.CreateVersion7(),
            executionContext.CorrelationId,
            order.TenantId,
            actorId,
            requestedAt,
            PriceAdjustmentCommandSupport.CreateEvent(
                order,
                adjustment,
                "REQUESTED",
                requestedAt));
        outbox.Add(envelope, order.Id.ToString("D"));
        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(new RequestPriceAdjustmentResponse(
            adjustment.Id,
            order.Id,
            PriceAdjustmentCommandSupport.MapStatus(adjustment.Status),
            adjustment.OldPoleCount,
            adjustment.NewPoleCount,
            adjustment.OldSurveyServicePriceId,
            adjustment.NewSurveyServicePriceId,
            adjustment.OldPricePerPole,
            adjustment.NewPricePerPole,
            adjustment.OldFinalPrice,
            adjustment.NewFinalPrice,
            adjustment.Currency,
            adjustment.Reason,
            adjustment.EvidenceReference,
            adjustment.RequestedBy,
            adjustment.Version,
            order.Version,
            adjustment.CreatedAt));
    }

    private static bool IsConflict(string? sqlState) => sqlState is
        PostgresErrorCodes.UniqueViolation or
        PostgresErrorCodes.SerializationFailure or
        PostgresErrorCodes.DeadlockDetected;

    private static Result<RequestPriceAdjustmentResponse> Conflict() =>
        Result.Failure<RequestPriceAdjustmentResponse>(
            PriceAdjustmentError.PersistenceConflict());
}
