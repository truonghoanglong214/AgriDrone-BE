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

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.RequestExtraFlight;

internal sealed class RequestExtraFlightCommandHandler(
    IExtraFlightRequestRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<RequestExtraFlightCommand, Result<ExtraFlightRequestResponse>>
{
    public async Task<Result<ExtraFlightRequestResponse>> Handle(
        RequestExtraFlightCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ExtraFlightRequestResponse>(ExtraFlightRequestError.Forbidden());
        }

        var now = timeProvider.GetUtcNow();
        ExtraFlightRequest request;
        try
        {
            request = ExtraFlightRequest.Create(
                command.SurveyOrderId,
                command.FarmId,
                command.SourceMissionId,
                command.Reason,
                command.EvidenceReference,
                command.CostImpact,
                CurrencyCode.Create(command.Currency),
                actorId,
                now);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<ExtraFlightRequestResponse>(ExtraFlightRequestError.Conflict(ex.Message));
        }

        repository.Add(request);

        using var data = JsonSerializer.SerializeToDocument(new
        {
            request.Id,
            request.SurveyOrderId,
            request.FarmId,
            request.Reason,
            request.CostImpact,
            request.Currency,
            request.Status
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(ExtraFlightRequest),
            request.Id,
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
            return Result.Failure<ExtraFlightRequestResponse>(
                ExtraFlightRequestError.Conflict("Persistence conflict occurred while requesting extra flight."));
        }

        return Result.Success(Map(request));
    }

    internal static ExtraFlightRequestResponse Map(ExtraFlightRequest item) =>
        new(item.Id, item.SurveyOrderId, item.FarmId, item.SourceMissionId,
            item.ExtraMissionId, item.Reason, item.EvidenceReference, item.CostImpact,
            item.Currency, item.Status, item.RequestedBy, item.ReviewedBy, item.ReviewedAt,
            item.Version, item.CreatedAt);
}
