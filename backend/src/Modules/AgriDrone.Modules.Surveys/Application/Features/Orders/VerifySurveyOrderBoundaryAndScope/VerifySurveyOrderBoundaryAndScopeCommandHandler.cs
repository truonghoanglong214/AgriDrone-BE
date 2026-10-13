using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;

internal sealed class VerifySurveyOrderBoundaryAndScopeCommandHandler(
    ISurveyBoundaryVerificationUnitOfWork unitOfWork,
    ISurveyOrderBoundaryRepository boundaryRepository,
    ISurveyBoundaryVerificationOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<VerifySurveyOrderBoundaryAndScopeCommand,
        Result<VerifySurveyOrderBoundaryAndScopeResponse>>
{
    public async Task<Result<VerifySurveyOrderBoundaryAndScopeResponse>> Handle(
        VerifySurveyOrderBoundaryAndScopeCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<VerifySurveyOrderBoundaryAndScopeResponse>(
                SurveyOrderError.Forbidden());
        }

        var verifiedAt = timeProvider.GetUtcNow();

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                transactionCancellationToken => VerifyWithinTransactionAsync(
                    command,
                    actorId,
                    verifiedAt,
                    transactionCancellationToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<VerifySurveyOrderBoundaryAndScopeResponse>(
                SurveyBoundaryVerificationError.PersistenceConflict());
        }
        catch (PostgresException exception)
            when (exception.SqlState is
                PostgresErrorCodes.UniqueViolation or
                PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected)
        {
            return Result.Failure<VerifySurveyOrderBoundaryAndScopeResponse>(
                SurveyBoundaryVerificationError.PersistenceConflict());
        }
        catch (DbUpdateException exception)
            when (exception.HasPostgresSqlState(
                      PostgresErrorCodes.UniqueViolation) ||
                  exception.HasPostgresSqlState(
                      PostgresErrorCodes.SerializationFailure) ||
                  exception.HasPostgresSqlState(
                      PostgresErrorCodes.DeadlockDetected))
        {
            return Result.Failure<VerifySurveyOrderBoundaryAndScopeResponse>(
                SurveyBoundaryVerificationError.PersistenceConflict());
        }
    }

    private async Task<Result<VerifySurveyOrderBoundaryAndScopeResponse>>
        VerifyWithinTransactionAsync(
            VerifySurveyOrderBoundaryAndScopeCommand command,
            Guid actorId,
            DateTimeOffset verifiedAt,
            CancellationToken cancellationToken)
    {
        var stateResult = await boundaryRepository.GetForVerificationAsync(
            command.SurveyOrderId,
            actorId,
            verifiedAt,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<VerifySurveyOrderBoundaryAndScopeResponse>(
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
            return Result.Failure<VerifySurveyOrderBoundaryAndScopeResponse>(
                MapOrderDomainError(exception));
        }

        var boundaryResult = await boundaryRepository.StageApprovedBoundaryAsync(
            order,
            command.Boundary,
            command.ExpectedCurrentBoundaryVersionNumber,
            actorId,
            command.Reason,
            command.EvidenceReference,
            verifiedAt,
            cancellationToken);
        if (boundaryResult.IsFailure)
        {
            return Result.Failure<VerifySurveyOrderBoundaryAndScopeResponse>(
                boundaryResult.Error);
        }

        try
        {
            order.VerifyBoundaryAndScope(
                boundaryResult.Value.FarmBoundaryId,
                state.CurrentPublishedBaseMapVersionId,
                state.CurrentConfirmedPoleCount.HasValue
                    ? ConfirmedSurveyPoleCount.Create(
                        state.CurrentConfirmedPoleCount.Value)
                    : null,
                actorId,
                verifiedAt);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<VerifySurveyOrderBoundaryAndScopeResponse>(
                MapOrderDomainError(exception));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            status = SurveyOrderStatus.PendingBoundaryVerification.ToString(),
            expectedOrderVersion = command.ExpectedOrderVersion,
            expectedCurrentBoundaryVersionNumber =
                command.ExpectedCurrentBoundaryVersionNumber
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            boundaryId = boundaryResult.Value.FarmBoundaryId,
            boundaryVersionNumber = boundaryResult.Value.VersionNumber,
            orderStatus = order.Status.ToString(),
            order.RequiresBaselineMapping,
            currentFarmBaseMapVersionId =
                state.CurrentPublishedBaseMapVersionId,
            confirmedSurveyPoleCount = state.CurrentConfirmedPoleCount,
            evidenceReference = string.IsNullOrWhiteSpace(
                command.EvidenceReference)
                ? null
                : command.EvidenceReference.Trim()
        });

        auditWriter.AddUserAction(
            unitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            executionContext.CorrelationId,
            "SurveyOrder",
            order.Id,
            "VERIFY_BOUNDARY_SCOPE",
            oldData,
            newData,
            verifiedAt);

        var boundaryVerified = new SurveyOrderBoundaryVerifiedV1(
            order.Id,
            order.TenantId,
            order.FarmId,
            boundaryResult.Value.FarmBoundaryId,
            boundaryResult.Value.VersionNumber,
            state.SystemManagerProfileId,
            order.RequiresBaselineMapping,
            state.CurrentPublishedBaseMapVersionId,
            state.CurrentConfirmedPoleCount,
            MapOrderStatus(order.Status),
            verifiedAt);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyOrderBoundaryVerifiedV1,
            Guid.CreateVersion7(),
            executionContext.CorrelationId,
            order.TenantId,
            actorId,
            verifiedAt,
            boundaryVerified);
        outbox.Add(envelope, order.Id.ToString("D"));

        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(new VerifySurveyOrderBoundaryAndScopeResponse(
            order.Id,
            order.OrderNumber,
            order.TenantId,
            order.FarmId,
            boundaryResult.Value.FarmBoundaryId,
            boundaryResult.Value.VersionNumber,
            state.SystemManagerProfileId,
            order.RequiresBaselineMapping,
            state.CurrentPublishedBaseMapVersionId,
            state.CurrentConfirmedPoleCount,
            order.Status,
            order.Version,
            verifiedAt));
    }

    private static AppError MapOrderDomainError(
        SurveyDomainException exception) =>
        exception.Code switch
        {
            SurveyOrderDomainErrorCodes.VersionConflict =>
                SurveyOrderError.VersionConflict(),
            SurveyOrderDomainErrorCodes.InvalidTransition =>
                SurveyOrderError.InvalidTransition(exception.Message),
            SurveyOrderDomainErrorCodes.BaselineSnapshotIncomplete =>
                SurveyBoundaryVerificationError.FarmInventoryIncomplete(),
            _ => AppError.Conflict(exception.Code, exception.Message)
        };

    private static string MapOrderStatus(SurveyOrderStatus status) =>
        status switch
        {
            SurveyOrderStatus.AwaitingBaselineAppointment =>
                "AWAITING_BASELINE_APPOINTMENT",
            SurveyOrderStatus.AwaitingPricing => "AWAITING_PRICING",
            _ => throw new InvalidOperationException(
                "Boundary verification produced an unsupported order status.")
        };
}
