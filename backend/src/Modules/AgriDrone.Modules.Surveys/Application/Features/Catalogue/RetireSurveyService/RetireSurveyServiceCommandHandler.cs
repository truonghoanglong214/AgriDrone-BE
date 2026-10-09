using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.RetireSurveyService;

internal sealed class RetireSurveyServiceCommandHandler(
    ISurveyServiceRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        RetireSurveyServiceCommand,
        Result<RetireSurveyServiceResponse>>
{
    public async Task<Result<RetireSurveyServiceResponse>> Handle(
        RetireSurveyServiceCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<RetireSurveyServiceResponse>(
                SurveyServiceError.CurrentUserRequired());
        }

        var service = await repository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);
        if (service is null)
        {
            return Result.Failure<RetireSurveyServiceResponse>(
                SurveyServiceError.NotFound());
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = Snapshot(service, reason: null);
        try
        {
            service.Retire(now, request.ExpectedVersion);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<RetireSurveyServiceResponse>(
                MapDomainError(exception));
        }

        using var newData = Snapshot(service, request.Reason.Trim());
        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            "SurveyService",
            service.Id,
            "RETIRE",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<RetireSurveyServiceResponse>(
                SurveyServiceError.ConcurrentUpdate());
        }

        return Result.Success(new RetireSurveyServiceResponse(
            service.Id,
            service.Status,
            service.Version,
            service.UpdatedAt));
    }

    private static JsonDocument Snapshot(
        SurveyService service,
        string? reason) =>
        JsonSerializer.SerializeToDocument(new
        {
            service.Code,
            Status = service.Status.ToString(),
            Reason = reason,
            service.Version,
            service.UpdatedAt
        });

    private static AppError MapDomainError(SurveyDomainException exception) =>
        exception.Code == SurveyServiceDomainErrorCodes.VersionConflict
            ? SurveyServiceError.ConcurrentUpdate()
            : SurveyServiceError.InvalidLifecycle(exception.Message);
}
