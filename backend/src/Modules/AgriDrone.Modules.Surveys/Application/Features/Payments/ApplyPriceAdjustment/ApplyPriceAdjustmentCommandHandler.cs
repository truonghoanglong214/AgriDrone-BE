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

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ApplyPriceAdjustment;

internal sealed class ApplyPriceAdjustmentCommandHandler(
    ISurveyPaymentProcessingUnitOfWork unitOfWork,
    ISurveyPaymentProcessingRepository repository,
    ISurveyPaymentProcessingOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ApplyPriceAdjustmentCommand,
        Result<ApplyPriceAdjustmentResponse>>
{
    public async Task<Result<ApplyPriceAdjustmentResponse>> Handle(
        ApplyPriceAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ApplyPriceAdjustmentResponse>(
                PriceAdjustmentError.Forbidden());
        }

        var appliedAt = timeProvider.GetUtcNow();
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    var stateResult = await repository
                        .GetForPriceAdjustmentReviewAsync(
                            command.SurveyOrderId,
                            command.PriceAdjustmentId,
                            token);
                    if (stateResult.IsFailure)
                    {
                        return Result.Failure<ApplyPriceAdjustmentResponse>(
                            stateResult.Error);
                    }

                    var state = stateResult.Value;
                    var order = state.Order;
                    var adjustment = state.Adjustment;
                    var payment = state.ActivePayment;
                    var previousOrderStatus = order.Status;
                    var previousPaymentStatus = payment?.Status;
                    try
                    {
                        order.EnsureVersion(command.ExpectedOrderVersion);
                        adjustment.EnsureVersion(
                            command.ExpectedAdjustmentVersion);
                        if (payment is not null)
                        {
                            if (!command.ExpectedPaymentVersion.HasValue)
                            {
                                return Result.Failure<ApplyPriceAdjustmentResponse>(
                                    PaymentError.VersionConflict());
                            }

                            payment.EnsureVersion(
                                command.ExpectedPaymentVersion.Value);
                        }

                        if (!MatchesOldSnapshot(order, adjustment) ||
                            payment?.Status is SurveyPaymentStatus.Pending or
                                SurveyPaymentStatus.Processing)
                        {
                            return Result.Failure<ApplyPriceAdjustmentResponse>(
                                PriceAdjustmentError.OrderNotEligible());
                        }

                        order.ApplyPriceAdjustment(
                            adjustment.NewSurveyServicePriceId,
                            ConfirmedSurveyPoleCount.Create(
                                adjustment.NewPoleCount),
                            PricePerPole.Create(
                                adjustment.NewPricePerPole),
                            CurrencyCode.Create(adjustment.Currency),
                            actorId,
                            appliedAt);
                        if (payment?.Status == SurveyPaymentStatus.Paid &&
                            payment.Purpose == PaymentPurpose.FinalBalance &&
                            (payment.Amount != (order.RemainingAmount ?? adjustment.NewFinalPrice) ||
                             !string.Equals(
                                 payment.Currency,
                                 adjustment.Currency,
                                 StringComparison.Ordinal)))
                        {
                            payment.RequireAdjustment(appliedAt);
                        }

                        adjustment.MarkApplied(actorId, appliedAt);
                    }
                    catch (SurveyDomainException exception)
                    {
                        return Result.Failure<ApplyPriceAdjustmentResponse>(
                            PriceAdjustmentCommandSupport.MapDomainError(
                                exception));
                    }

                    using var oldData = JsonSerializer.SerializeToDocument(new
                    {
                        adjustment.OldPoleCount,
                        adjustment.OldSurveyServicePriceId,
                        adjustment.OldPricePerPole,
                        adjustment.OldFinalPrice,
                        orderStatus = SurveyPaymentCommandSupport.MapOrderStatus(
                            previousOrderStatus),
                        paymentStatus = previousPaymentStatus is null
                            ? null
                            : SurveyPaymentCommandSupport.MapStatus(
                                previousPaymentStatus.Value)
                    });
                    using var newData = JsonSerializer.SerializeToDocument(new
                    {
                        adjustment.NewPoleCount,
                        adjustment.NewSurveyServicePriceId,
                        adjustment.NewPricePerPole,
                        adjustment.NewFinalPrice,
                        adjustment.Currency,
                        orderStatus = SurveyPaymentCommandSupport.MapOrderStatus(
                            order.Status),
                        paymentStatus = payment is null
                            ? null
                            : SurveyPaymentCommandSupport.MapStatus(
                                payment.Status)
                    });
                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        nameof(PriceAdjustment),
                        adjustment.Id,
                        "APPLY_PRICE_ADJUSTMENT",
                        oldData,
                        newData,
                        appliedAt);
                    var adjustmentEnvelope = IntegrationEventEnvelopeFactory
                        .Create(
                            IntegrationEventDescriptors
                                .SurveyPriceAdjustmentChangedV1,
                            Guid.CreateVersion7(),
                            executionContext.CorrelationId,
                            order.TenantId,
                            actorId,
                            appliedAt,
                            PriceAdjustmentCommandSupport.CreateEvent(
                                order,
                                adjustment,
                                "APPLIED",
                                appliedAt));
                    outbox.Add(
                        adjustmentEnvelope,
                        order.Id.ToString("D"));
                    if (payment is not null &&
                        previousPaymentStatus != payment.Status)
                    {
                        var paymentEnvelope = IntegrationEventEnvelopeFactory
                            .Create(
                                IntegrationEventDescriptors
                                    .SurveyPaymentChangedV1,
                                Guid.CreateVersion7(),
                                executionContext.CorrelationId,
                                order.TenantId,
                                actorId: null,
                                appliedAt,
                                SurveyPaymentCommandSupport.CreateEvent(
                                    order,
                                    payment,
                                    "PRICE_ADJUSTMENT_APPLIED",
                                    "COMMERCIAL_GATE_RECHECK_REQUIRED",
                                    appliedAt));
                        outbox.Add(paymentEnvelope, order.Id.ToString("D"));
                    }

                    await unitOfWork.FlushAsync(token);
                    return Result.Success(new ApplyPriceAdjustmentResponse(
                        adjustment.Id,
                        order.Id,
                        PriceAdjustmentCommandSupport.MapStatus(
                            adjustment.Status),
                        order.ConfirmedSurveyPoleCount!.Value.Value,
                        order.SurveyServicePriceId!.Value,
                        order.PricePerPoleSnapshot!.Value.Amount,
                        order.FinalPrice!.Value,
                        order.Currency!,
                        SurveyPaymentCommandSupport.MapOrderStatus(order.Status),
                        payment is null
                            ? null
                            : SurveyPaymentCommandSupport.MapStatus(
                                payment.Status),
                        adjustment.Version,
                        order.Version,
                        payment?.Version,
                        actorId,
                        appliedAt));
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

    private static bool MatchesOldSnapshot(
        SurveyOrder order,
        PriceAdjustment adjustment) =>
        order.ConfirmedSurveyPoleCount?.Value == adjustment.OldPoleCount &&
        order.SurveyServicePriceId == adjustment.OldSurveyServicePriceId &&
        order.PricePerPoleSnapshot?.Amount == adjustment.OldPricePerPole &&
        order.FinalPrice == adjustment.OldFinalPrice &&
        string.Equals(
            order.Currency,
            adjustment.Currency,
            StringComparison.Ordinal);

    private static Result<ApplyPriceAdjustmentResponse> Conflict() =>
        Result.Failure<ApplyPriceAdjustmentResponse>(
            PriceAdjustmentError.PersistenceConflict());
}
