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

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.RejectPriceAdjustment;

internal sealed class RejectPriceAdjustmentCommandHandler(
    ISurveyPaymentProcessingUnitOfWork unitOfWork,
    ISurveyPaymentProcessingRepository repository,
    ISurveyPaymentProcessingOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RejectPriceAdjustmentCommand,
        Result<RejectPriceAdjustmentResponse>>
{
    public async Task<Result<RejectPriceAdjustmentResponse>> Handle(
        RejectPriceAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<RejectPriceAdjustmentResponse>(
                PriceAdjustmentError.Forbidden());
        }

        var reviewedAt = timeProvider.GetUtcNow();
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
                        return Result.Failure<RejectPriceAdjustmentResponse>(
                            stateResult.Error);
                    }

                    var state = stateResult.Value;
                    try
                    {
                        state.Order.EnsureVersion(command.ExpectedOrderVersion);
                        state.Adjustment.EnsureVersion(
                            command.ExpectedAdjustmentVersion);
                        state.Adjustment.Reject(
                            actorId,
                            command.Reason,
                            command.EvidenceReference,
                            reviewedAt);
                    }
                    catch (SurveyDomainException exception)
                    {
                        return Result.Failure<RejectPriceAdjustmentResponse>(
                            PriceAdjustmentCommandSupport.MapDomainError(
                                exception));
                    }

                    using var oldData = JsonSerializer.SerializeToDocument(new
                    {
                        status = "PENDING"
                    });
                    using var newData = JsonSerializer.SerializeToDocument(new
                    {
                        status = PriceAdjustmentCommandSupport.MapStatus(
                            state.Adjustment.Status),
                        state.Adjustment.ReviewReason,
                        state.Adjustment.ReviewEvidenceReference
                    });
                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        nameof(PriceAdjustment),
                        state.Adjustment.Id,
                        "REJECT_PRICE_ADJUSTMENT",
                        oldData,
                        newData,
                        reviewedAt);
                    var envelope = IntegrationEventEnvelopeFactory.Create(
                        IntegrationEventDescriptors
                            .SurveyPriceAdjustmentChangedV1,
                        Guid.CreateVersion7(),
                        executionContext.CorrelationId,
                        state.Order.TenantId,
                        actorId,
                        reviewedAt,
                        PriceAdjustmentCommandSupport.CreateEvent(
                            state.Order,
                            state.Adjustment,
                            "REJECTED",
                            reviewedAt));
                    outbox.Add(envelope, state.Order.Id.ToString("D"));
                    await unitOfWork.FlushAsync(token);
                    return Result.Success(new RejectPriceAdjustmentResponse(
                        state.Adjustment.Id,
                        state.Order.Id,
                        PriceAdjustmentCommandSupport.MapStatus(
                            state.Adjustment.Status),
                        actorId,
                        state.Adjustment.ReviewReason!,
                        state.Adjustment.ReviewEvidenceReference!,
                        state.Adjustment.Version,
                        state.Order.Version,
                        reviewedAt));
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

    private static Result<RejectPriceAdjustmentResponse> Conflict() =>
        Result.Failure<RejectPriceAdjustmentResponse>(
            PriceAdjustmentError.PersistenceConflict());
}
