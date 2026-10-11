using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.ConfirmSurveyOrderPoleCount;

internal sealed class ConfirmSurveyOrderPoleCountCommandHandler(
    ISurveyPricingConfirmationUnitOfWork unitOfWork,
    ISurveyPricingConfirmationRepository repository,
    ISurveyPricingConfirmationOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ConfirmSurveyOrderPoleCountCommand,
        Result<ConfirmSurveyOrderPoleCountResponse>>
{
    public async Task<Result<ConfirmSurveyOrderPoleCountResponse>> Handle(
        ConfirmSurveyOrderPoleCountCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<ConfirmSurveyOrderPoleCountResponse>(
                SurveyPricingError.Forbidden());
        }

        var confirmedAt = timeProvider.GetUtcNow();
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(
                token => ConfirmWithinTransactionAsync(
                    command,
                    actorId,
                    confirmedAt,
                    token),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }
        catch (PostgresException exception) when (exception.SqlState is
            PostgresErrorCodes.UniqueViolation or
            PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected)
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

    private async Task<Result<ConfirmSurveyOrderPoleCountResponse>>
        ConfirmWithinTransactionAsync(
            ConfirmSurveyOrderPoleCountCommand command,
            Guid actorId,
            DateTimeOffset confirmedAt,
            CancellationToken cancellationToken)
    {
        var stateResult = await repository.GetForConfirmationAsync(
            command.SurveyOrderId,
            actorId,
            confirmedAt,
            cancellationToken);
        if (stateResult.IsFailure)
        {
            return Result.Failure<ConfirmSurveyOrderPoleCountResponse>(
                stateResult.Error);
        }

        var state = stateResult.Value;
        var order = state.Order;
        if (state.CurrentFarmBaseMapVersionId !=
            command.ExpectedFarmBaseMapVersionId)
        {
            return Result.Failure<ConfirmSurveyOrderPoleCountResponse>(
                SurveyPricingError.BaseMapVersionConflict());
        }

        var poleCountSource = state.BaseMapSourceSurveyOrderId == order.Id
            ? SurveyPoleCountSource.BaselinePublication
            : SurveyPoleCountSource.CurrentPublishedInventory;
        if (order.RequiresBaselineMapping !=
            (poleCountSource == SurveyPoleCountSource.BaselinePublication))
        {
            return Result.Failure<ConfirmSurveyOrderPoleCountResponse>(
                SurveyPricingError.BaselineSourceMismatch());
        }

        var oldMapVersionId = order.FarmBaseMapVersionId;
        var oldPoleCount = order.ConfirmedSurveyPoleCount?.Value;
        try
        {
            order.EnsureVersion(command.ExpectedOrderVersion);
            order.ConfirmPoleCountAndPricing(
                state.CurrentFarmBaseMapVersionId,
                ConfirmedSurveyPoleCount.Create(
                    state.AuthoritativeActivePoleCount),
                poleCountSource,
                command.EvidenceReference,
                state.SurveyServicePriceId,
                PricePerPole.Create(state.PricePerPole),
                CurrencyCode.Create(state.Currency),
                actorId,
                confirmedAt);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<ConfirmSurveyOrderPoleCountResponse>(
                MapDomainError(exception));
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            orderStatus = SurveyOrderStatus.AwaitingPricing.ToString(),
            farmBaseMapVersionId = oldMapVersionId,
            confirmedSurveyPoleCount = oldPoleCount,
            expectedOrderVersion = command.ExpectedOrderVersion
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            order.FarmBaseMapVersionId,
            poleCountSource = MapSource(poleCountSource),
            order.PoleCountEvidenceReference,
            confirmedSurveyPoleCount = order.ConfirmedSurveyPoleCount!.Value.Value,
            order.SurveyServicePriceId,
            pricePerPole = order.PricePerPoleSnapshot!.Value.Amount,
            order.Currency,
            order.FinalPrice,
            orderStatus = order.Status.ToString()
        });
        auditWriter.AddUserAction(
            unitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(SurveyOrder),
            order.Id,
            "CONFIRM_POLE_COUNT_AND_PRICING",
            oldData,
            newData,
            confirmedAt);

        var pricingConfirmed = new SurveyOrderPricingConfirmedV1(
            order.Id,
            order.TenantId,
            order.FarmId,
            order.SurveyServiceId,
            order.FarmBaseMapVersionId!.Value,
            MapSource(poleCountSource),
            order.ConfirmedSurveyPoleCount!.Value.Value,
            order.SurveyServicePriceId!.Value,
            order.PricePerPoleSnapshot!.Value.Amount,
            order.Currency!,
            order.FinalPrice!.Value,
            state.SystemManagerProfileId,
            confirmedAt);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyOrderPricingConfirmedV1,
            Guid.CreateVersion7(),
            executionContext.CorrelationId,
            order.TenantId,
            actorId,
            confirmedAt,
            pricingConfirmed);
        outbox.Add(envelope, order.Id.ToString("D"));

        await unitOfWork.FlushAsync(cancellationToken);

        return Result.Success(new ConfirmSurveyOrderPoleCountResponse(
            order.Id,
            order.OrderNumber,
            order.TenantId,
            order.FarmId,
            order.SurveyServiceId,
            order.FarmBoundaryVersionId!.Value,
            order.FarmBaseMapVersionId.Value,
            poleCountSource,
            order.PoleCountEvidenceReference!,
            order.ConfirmedSurveyPoleCount.Value.Value,
            order.SurveyServicePriceId.Value,
            order.PricePerPoleSnapshot.Value.Amount,
            order.Currency!,
            order.FinalPrice.Value,
            state.SystemManagerProfileId,
            order.Status,
            order.Version,
            confirmedAt));
    }

    private static AppError MapDomainError(SurveyDomainException exception) =>
        exception.Code switch
        {
            SurveyOrderDomainErrorCodes.VersionConflict =>
                SurveyOrderError.VersionConflict(),
            SurveyOrderDomainErrorCodes.InvalidTransition =>
                SurveyOrderError.InvalidTransition(exception.Message),
            SurveyOrderDomainErrorCodes.PricingSnapshotIncomplete =>
                AppError.Conflict(exception.Code, exception.Message),
            _ => AppError.Conflict(exception.Code, exception.Message)
        };

    private static string MapSource(SurveyPoleCountSource source) => source switch
    {
        SurveyPoleCountSource.BaselinePublication => "BASELINE_PUBLICATION",
        SurveyPoleCountSource.CurrentPublishedInventory =>
            "CURRENT_PUBLISHED_INVENTORY",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private static Result<ConfirmSurveyOrderPoleCountResponse> Conflict() =>
        Result.Failure<ConfirmSurveyOrderPoleCountResponse>(
            SurveyPricingError.PersistenceConflict());
}
