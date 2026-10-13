using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.UpdateSurveyServiceMetadata;

internal sealed class UpdateSurveyServiceMetadataCommandHandler(
    ISurveyServiceRepository repository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        UpdateSurveyServiceMetadataCommand,
        Result<UpdateSurveyServiceMetadataResponse>>
{
    public async Task<Result<UpdateSurveyServiceMetadataResponse>> Handle(
        UpdateSurveyServiceMetadataCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<UpdateSurveyServiceMetadataResponse>(
                SurveyServiceError.CurrentUserRequired());
        }

        var service = await repository.GetByIdAsync(
            request.SurveyServiceId,
            cancellationToken);
        if (service is null)
        {
            return Result.Failure<UpdateSurveyServiceMetadataResponse>(
                SurveyServiceError.NotFound());
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = Snapshot(service);
        try
        {
            service.UpdateMetadata(
                request.Name,
                request.Description,
                now,
                request.ExpectedVersion);
        }
        catch (SurveyDomainException exception)
        {
            return Result.Failure<UpdateSurveyServiceMetadataResponse>(
                MapDomainError(exception));
        }

        using var newData = Snapshot(service);
        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            "SurveyService",
            service.Id,
            "UPDATE_METADATA",
            oldData,
            newData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<UpdateSurveyServiceMetadataResponse>(
                SurveyServiceError.ConcurrentUpdate());
        }

        return Result.Success(new UpdateSurveyServiceMetadataResponse(
            service.Id,
            service.Code,
            service.Name,
            service.Description,
            service.Status,
            service.Version,
            service.UpdatedAt));
    }

    private static JsonDocument Snapshot(SurveyService service) =>
        JsonSerializer.SerializeToDocument(new
        {
            service.Code,
            service.Name,
            service.Description,
            Status = service.Status.ToString(),
            service.Version,
            service.UpdatedAt
        });

    private static AppError MapDomainError(SurveyDomainException exception) =>
        exception.Code == SurveyServiceDomainErrorCodes.VersionConflict
            ? SurveyServiceError.ConcurrentUpdate()
            : SurveyServiceError.InvalidLifecycle(exception.Message);
}
