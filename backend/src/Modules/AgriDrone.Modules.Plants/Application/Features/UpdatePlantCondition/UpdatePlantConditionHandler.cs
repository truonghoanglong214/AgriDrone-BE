using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using System.Text.Json;

namespace AgriDrone.Modules.Plants.Application.Features.UpdatePlantCondition;

internal sealed class UpdatePlantConditionHandler(
    IPlantConditionRepository plantConditionRepository,
    IPlantsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<UpdatePlantConditionCommand, Result<PlantConditionResponse>>
{
    public async Task<Result<PlantConditionResponse>> Handle(
        UpdatePlantConditionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.CurrentUserRequired());
        }

        var current = await plantConditionRepository.GetByIdAsync(
            request.ConditionId,
            cancellationToken);

        if (current is null)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.NotFound());
        }

        if (!current.IsActive)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.AlreadyRetired());
        }

        if (current.Version != request.ExpectedVersion)
        {
            return Result.Failure<PlantConditionResponse>(
                PlantConditionError.ConcurrentUpdate());
        }

        var now = timeProvider.GetUtcNow();
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            current.Name,
            current.ScientificName,
            current.Description,
            current.DatasetVersion,
            current.AnnotationRuleVersion,
            current.TeamLabelLimitation,
            current.RevisionNumber,
            current.IsActive
        });

        current.UpdateDetails(
            request.Name,
            request.ScientificName,
            request.Description,
            now);

        plantConditionRepository.Update(current);

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            current.Name,
            current.ScientificName,
            current.Description,
            current.DatasetVersion,
            current.AnnotationRuleVersion,
            current.TeamLabelLimitation,
            current.RevisionNumber,
            current.IsActive
        });

        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            "PlantCondition",
            current.Id,
            "UPDATE",
            oldData,
            newData,
            now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(PlantConditionResponse.From(current));
    }
}
