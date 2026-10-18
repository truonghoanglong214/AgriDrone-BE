using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.CreateAdjustmentSettlement;

internal sealed class CreateAdjustmentSettlementCommandHandler(
    IAdjustmentSettlementRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<CreateAdjustmentSettlementCommand, Result<AdjustmentSettlementResponse>>
{
    public async Task<Result<AdjustmentSettlementResponse>> Handle(
        CreateAdjustmentSettlementCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<AdjustmentSettlementResponse>(
                AppError.Forbidden("AdjustmentSettlement.Forbidden", "Actor is not authorized."));
        }

        var existing = await repository.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.PriceAdjustmentId == command.PriceAdjustmentId &&
                existing.Type == command.Type &&
                existing.Amount == Math.Round(command.Amount, 2, MidpointRounding.AwayFromZero) &&
                string.Equals(existing.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(Map(existing));
            }

            return Result.Failure<AdjustmentSettlementResponse>(
                AdjustmentSettlementError.IdempotencyConflict(command.IdempotencyKey));
        }

        var now = timeProvider.GetUtcNow();
        AdjustmentSettlement settlement;
        try
        {
            settlement = AdjustmentSettlement.Create(
                command.PriceAdjustmentId,
                command.Type,
                command.Amount,
                CurrencyCode.Create(command.Currency),
                command.IdempotencyKey,
                now);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<AdjustmentSettlementResponse>(AdjustmentSettlementError.Conflict(ex.Message));
        }

        repository.Add(settlement);

        using var data = JsonSerializer.SerializeToDocument(new
        {
            settlement.Id,
            settlement.PriceAdjustmentId,
            settlement.Type,
            settlement.Amount,
            settlement.Currency,
            settlement.Status
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(AdjustmentSettlement),
            settlement.Id,
            "CREATE",
            null,
            data,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<AdjustmentSettlementResponse>(
                AdjustmentSettlementError.Conflict("Persistence conflict occurred while creating settlement."));
        }

        return Result.Success(Map(settlement));
    }

    internal static AdjustmentSettlementResponse Map(AdjustmentSettlement item) =>
        new(item.Id, item.PriceAdjustmentId, item.Type, item.Status, item.Amount,
            item.Currency, item.IdempotencyKey, item.ProviderReference, item.FailureReason,
            item.Version, item.CreatedAt, item.UpdatedAt);
}
