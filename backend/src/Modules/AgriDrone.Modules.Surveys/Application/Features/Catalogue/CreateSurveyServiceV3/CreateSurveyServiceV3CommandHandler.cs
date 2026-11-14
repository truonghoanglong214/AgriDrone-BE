using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServiceV3;

internal sealed class CreateSurveyServiceV3CommandHandler(
    ISurveyServiceRepository serviceRepository,
    ISurveysUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSurveyServiceV3Command, Result<CreateSurveyServiceV3Response>>
{
    public async Task<Result<CreateSurveyServiceV3Response>> Handle(
        CreateSurveyServiceV3Command request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<CreateSurveyServiceV3Response>(
                BusinessPolicyError.CurrentUserRequired());
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var existing = await serviceRepository.GetByCodeAsync(normalizedCode, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<CreateSurveyServiceV3Response>(
                BusinessPolicyError.DuplicateServiceCode(normalizedCode));
        }

        var now = timeProvider.GetUtcNow();
        SurveyService service;
        try
        {
            service = SurveyService.CreateV3(
                normalizedCode,
                request.Name,
                request.Description,
                now);

            if (request.ActivateImmediately)
            {
                service.Activate(now, expectedVersion: 0);
            }
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<CreateSurveyServiceV3Response>(
                BusinessPolicyError.InvalidLifecycle(ex.Message));
        }

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            service.Id,
            service.Code,
            service.Name,
            service.Description,
            Scope = service.Scope.ToString(),
            Status = service.Status.ToString(),
            service.CreatedAt
        });

        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                serviceRepository.Add(service);

                auditWriter.AddSystemAdminAction(
                    unitOfWork,
                    actorId,
                    executionContext.CorrelationId,
                    "SurveyService",
                    service.Id,
                    "CREATE_V3_SERVICE",
                    oldData: null,
                    auditData,
                    now);

                await unitOfWork.SaveChangesAsync(ct);
                return true;
            },
            cancellationToken);

        return Result.Success(new CreateSurveyServiceV3Response(
            service.Id,
            service.Code,
            service.Name,
            service.Description,
            service.Scope.ToString().ToUpperInvariant(),
            service.Status.ToString().ToUpperInvariant(),
            service.CreatedAt));
    }
}
