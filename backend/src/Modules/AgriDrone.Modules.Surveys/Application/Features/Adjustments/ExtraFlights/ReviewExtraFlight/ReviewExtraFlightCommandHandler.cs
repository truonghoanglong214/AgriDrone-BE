using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Adjustments;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.RequestExtraFlight;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.ReviewExtraFlight;

internal sealed class ReviewExtraFlightCommandHandler(
    IExtraFlightRequestRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ReviewExtraFlightCommand, Result<ExtraFlightRequestResponse>>
{
    public async Task<Result<ExtraFlightRequestResponse>> Handle(
        ReviewExtraFlightCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ExtraFlightRequestResponse>(ExtraFlightRequestError.Forbidden());
        }

        var request = await repository.GetAsync(command.SurveyOrderId, command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure<ExtraFlightRequestResponse>(ExtraFlightRequestError.NotFound(command.RequestId));
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            request.Status,
            request.Version
        });

        try
        {
            request.Review(command.Approved, actorId, now, command.ExpectedVersion);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<ExtraFlightRequestResponse>(ExtraFlightRequestError.Conflict(ex.Message));
        }

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            request.Status,
            request.ReviewedBy,
            request.ReviewedAt,
            request.Version
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(ExtraFlightRequest),
            request.Id,
            command.Approved ? "APPROVE" : "REJECT",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ExtraFlightRequestResponse>(
                ExtraFlightRequestError.Conflict("Extra-flight request version conflict."));
        }

        return Result.Success(RequestExtraFlightCommandHandler.Map(request));
    }
}
