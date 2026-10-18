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

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.RequestScopeAdjustment;

internal sealed class RequestScopeAdjustmentCommandHandler(
    IScopeAdjustmentRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RequestScopeAdjustmentCommand, Result<ScopeAdjustmentResponse>>
{
    public async Task<Result<ScopeAdjustmentResponse>> Handle(
        RequestScopeAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ScopeAdjustmentResponse>(ScopeAdjustmentError.Forbidden());
        }

        var openAdjustment = await repository.GetOpenAdjustmentAsync(command.SurveyOrderId, cancellationToken);
        if (openAdjustment is not null)
        {
            return Result.Failure<ScopeAdjustmentResponse>(ScopeAdjustmentError.OpenAdjustmentExists());
        }

        var now = timeProvider.GetUtcNow();
        ScopeAdjustment adjustment;
        try
        {
            adjustment = ScopeAdjustment.Create(
                command.SurveyOrderId,
                command.Reason,
                command.BeforeBoundaryVersionId,
                command.AfterBoundaryVersionId,
                ConfirmedSurveyPoleCount.Create(command.BeforePoleCount),
                ConfirmedSurveyPoleCount.Create(command.AfterPoleCount),
                command.FinancialImpact,
                CurrencyCode.Create(command.Currency),
                command.EvidenceReference,
                actorId,
                now);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<ScopeAdjustmentResponse>(ScopeAdjustmentError.Conflict(ex.Message));
        }

        repository.Add(adjustment);

        using var data = JsonSerializer.SerializeToDocument(new
        {
            adjustment.Id,
            adjustment.SurveyOrderId,
            adjustment.Reason,
            adjustment.BeforePoleCount,
            adjustment.AfterPoleCount,
            adjustment.FinancialImpact,
            adjustment.Currency,
            adjustment.Status
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(ScopeAdjustment),
            adjustment.Id,
            "CREATE",
            null,
            data,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ScopeAdjustmentResponse>(ScopeAdjustmentError.Conflict("Concurrency conflict occurred."));
        }

        return Result.Success(Map(adjustment));
    }

    internal static ScopeAdjustmentResponse Map(ScopeAdjustment item) =>
        new(item.Id, item.SurveyOrderId, item.Reason, item.BeforeBoundaryVersionId,
            item.AfterBoundaryVersionId, item.BeforePoleCount, item.AfterPoleCount,
            item.FinancialImpact, item.Currency, item.EvidenceReference, item.Status,
            item.RequestedBy, item.ReviewedBy, item.ReviewedAt, item.ReviewReason,
            item.Version, item.CreatedAt);
}
