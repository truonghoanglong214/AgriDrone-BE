using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.RequestScopeAdjustment;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.ApplyScopeAdjustment;

internal sealed class ApplyScopeAdjustmentCommandHandler(
    IScopeAdjustmentRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ApplyScopeAdjustmentCommand, Result<ScopeAdjustmentResponse>>
{
    public async Task<Result<ScopeAdjustmentResponse>> Handle(
        ApplyScopeAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ScopeAdjustmentResponse>(ScopeAdjustmentError.Forbidden());
        }

        var adjustment = await repository.GetAsync(command.SurveyOrderId, command.ScopeAdjustmentId, cancellationToken);
        if (adjustment is null)
        {
            return Result.Failure<ScopeAdjustmentResponse>(ScopeAdjustmentError.NotFound(command.ScopeAdjustmentId));
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            adjustment.Status,
            adjustment.Version
        });

        try
        {
            adjustment.MarkApplied(now, command.ExpectedAdjustmentVersion);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<ScopeAdjustmentResponse>(ScopeAdjustmentError.Conflict(ex.Message));
        }

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            adjustment.Status,
            adjustment.Version
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(ScopeAdjustment),
            adjustment.Id,
            "APPLY",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ScopeAdjustmentResponse>(ScopeAdjustmentError.Conflict("Adjustment version conflict."));
        }

        return Result.Success(RequestScopeAdjustmentCommandHandler.Map(adjustment));
    }
}
