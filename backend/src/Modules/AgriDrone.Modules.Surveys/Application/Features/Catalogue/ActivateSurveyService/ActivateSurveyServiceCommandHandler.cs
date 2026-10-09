using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ActivateSurveyService;

internal sealed class ActivateSurveyServiceCommandHandler(
    ISurveyServiceRepository repository,
    ISurveyCatalogueQueries catalogueQueries,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        ActivateSurveyServiceCommand,
        Result<ActivateSurveyServiceResponse>>
{
    public async Task<Result<ActivateSurveyServiceResponse>> Handle(
        ActivateSurveyServiceCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ActivateSurveyServiceResponse>(
                SurveyServiceError.CurrentUserRequired());
        }

        var service = await repository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);
        if (service is null)
        {
            return Result.Failure<ActivateSurveyServiceResponse>(
                SurveyServiceError.NotFound());
        }

        var now = timeProvider.GetUtcNow();
        var effectivePrice = await catalogueQueries
            .GetEffectivePerPolePriceAsync(service.Id, now, cancellationToken);
        if (effectivePrice is null ||
            !string.Equals(effectivePrice.Currency, "VND", StringComparison.Ordinal))
        {
            return Result.Failure<ActivateSurveyServiceResponse>(
                SurveyServiceError.ActivePriceRequired());
        }

        using var oldData = Snapshot(service, reason: null);
        try
        {
            service.Activate(now, request.ExpectedVersion);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<ActivateSurveyServiceResponse>(
                MapDomainError(exception));
        }

        using var newData = Snapshot(service, request.Reason.Trim());
        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            "SurveyService",
            service.Id,
            "ACTIVATE",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ActivateSurveyServiceResponse>(
                SurveyServiceError.ConcurrentUpdate());
        }

        return Result.Success(new ActivateSurveyServiceResponse(
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
