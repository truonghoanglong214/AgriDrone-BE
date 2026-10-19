using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.CreateAdjustmentSettlement;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.FailAdjustmentSettlement;

internal sealed class FailAdjustmentSettlementCommandHandler(
    IAdjustmentSettlementRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<FailAdjustmentSettlementCommand, Result<AdjustmentSettlementResponse>>
{
    public async Task<Result<AdjustmentSettlementResponse>> Handle(
        FailAdjustmentSettlementCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<AdjustmentSettlementResponse>(
                AppError.Forbidden("AdjustmentSettlement.Forbidden", "Actor is not authorized."));
        }

        var settlement = await repository.GetAsync(command.SettlementId, cancellationToken);
        if (settlement is null)
        {
            return Result.Failure<AdjustmentSettlementResponse>(
                AdjustmentSettlementError.NotFound(command.SettlementId));
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            settlement.Status,
            settlement.Version
        });

        try
        {
            settlement.Fail(command.FailureReason, now, command.ExpectedVersion);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<AdjustmentSettlementResponse>(AdjustmentSettlementError.Conflict(ex.Message));
        }

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            settlement.Status,
            settlement.FailureReason,
            settlement.UpdatedAt,
            settlement.Version
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(AdjustmentSettlement),
            settlement.Id,
            "FAIL",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AdjustmentSettlementResponse>(
                AdjustmentSettlementError.Conflict("Settlement version conflict."));
        }

        return Result.Success(CreateAdjustmentSettlementCommandHandler.Map(settlement));
    }
}
