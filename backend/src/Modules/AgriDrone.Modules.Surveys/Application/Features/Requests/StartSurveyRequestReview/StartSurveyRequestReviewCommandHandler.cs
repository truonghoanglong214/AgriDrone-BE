using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.StartSurveyRequestReview;

internal sealed class StartSurveyRequestReviewCommandHandler(
    ISurveyRequestRepository requestRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        StartSurveyRequestReviewCommand,
        Result<StartSurveyRequestReviewResponse>>
{
    public async Task<Result<StartSurveyRequestReviewResponse>> Handle(
        StartSurveyRequestReviewCommand request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAdminContext(out var actorId))
        {
            return Result.Failure<StartSurveyRequestReviewResponse>(
                SurveyRequestError.CurrentSystemAdminRequired());
        }

        var surveyRequest = await requestRepository.GetByIdAsync(
            request.SurveyRequestId,
            cancellationToken);
        if (surveyRequest is null)
        {
            return Result.Failure<StartSurveyRequestReviewResponse>(
                SurveyRequestError.NotFound(request.SurveyRequestId));
        }

        using var oldData = SurveyRequestReviewAuditSnapshot.Create(
            surveyRequest);
        var now = timeProvider.GetUtcNow();
        try
        {
            surveyRequest.EnsureVersion(request.ExpectedVersion);
            surveyRequest.StartReview(now);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<StartSurveyRequestReviewResponse>(
                MapDomainError(exception, surveyRequest.Id));
        }
        catch (ArgumentException exception)
        {
            return Result.Failure<StartSurveyRequestReviewResponse>(
                SurveyRequestError.InvalidInput(exception.Message));
        }

        using var newData = SurveyRequestReviewAuditSnapshot.Create(
            surveyRequest);
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    auditWriter.AddSystemAdminAction(
                        unitOfWork,
                        actorId,
                        executionContext.CorrelationId,
                        "SurveyRequest",
                        surveyRequest.Id,
                        "START_REVIEW",
                        oldData,
                        newData,
                        now);
                    await unitOfWork.SaveChangesAsync(
                        transactionCancellationToken);
                    return true;
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<StartSurveyRequestReviewResponse>(
                SurveyRequestError.VersionConflict());
        }

        return Result.Success(new StartSurveyRequestReviewResponse(
            surveyRequest.Id,
            surveyRequest.RequestNumber,
            surveyRequest.Status,
            surveyRequest.Version,
            surveyRequest.UpdatedAt));
    }

    private bool TryGetAdminContext(out Guid actorId)
    {
        if (executionContext.IsInitialized &&
            executionContext.ActorId is Guid currentActorId &&
            executionContext.CorrelationId != Guid.Empty)
        {
            actorId = currentActorId;
            return true;
        }

        actorId = Guid.Empty;
        return false;
    }

    private static AppError MapDomainError(
        SurveyDomainException exception,
        Guid requestId) =>
        exception.Code switch
        {
            SurveyRequestDomainErrorCodes.VersionConflict =>
                SurveyRequestError.VersionConflict(),
            SurveyRequestDomainErrorCodes.InvalidTransition =>
                SurveyRequestError.NotReviewable(requestId),
            _ => SurveyRequestError.InvalidTransition(exception.Message)
        };
}
