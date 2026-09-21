using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition
{
    internal sealed class CreatePlantConditionHandler(
        IPlantConditionRepository plantConditionRepository,
        IPlantsUnitOfWork plantUnitOfWork,
        IAuditWriter auditWriter,
        IExecutionContext executionContext,
        TimeProvider timeProvider) : IRequestHandler<CreatePlantConditionCommand, Result<PlantConditionResponse>>
    {
        private static readonly string[] CodeConstraints =
        [
            "uq_plant_conditions_active_code",
            "uq_plant_conditions_code_revision"
        ];

        public async Task<Result<PlantConditionResponse>> Handle(CreatePlantConditionCommand request, CancellationToken cancellationToken)
        {
            if (executionContext.ActorId is not Guid actorId)
                return Result.Failure<PlantConditionResponse>(PlantConditionError.CurrentUserRequired());

            var now = timeProvider.GetUtcNow();
            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            
            var existingCode = await plantConditionRepository.CodeExistsAsync(
                normalizedCode,
                cancellationToken);

            if(existingCode)
                return Result.Failure<PlantConditionResponse>(PlantConditionError.CodeAlreadyExists(normalizedCode));

            var plantCondition = PlantCondition.Create(
                request.Code,
                request.Name,
                request.ScientificName,
                request.ConditionType,
                request.Description,
                now);

            plantConditionRepository.Add(plantCondition);

            using var newData = JsonSerializer.SerializeToDocument(new
            {
                plantCondition.Code,
                plantCondition.Name,
                plantCondition.ScientificName,
                ConditionType = plantCondition.ConditionType.ToString(),
                plantCondition.Description,
                plantCondition.RevisionNumber,
                plantCondition.IsActive
            });

            auditWriter.AddSystemAdminAction(
                plantUnitOfWork,
                actorId,
                executionContext.CorrelationId,
                "PlantCondition",
                plantCondition.Id,
                "CREATE",
                oldData: null,
                newData,
                now);

            try
            {
                await plantUnitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.IsUniqueConstraintViolation(
                    CodeConstraints))
            {
                return Result.Failure<PlantConditionResponse>(PlantConditionError.CodeAlreadyExists(normalizedCode));
            }

            return Result.Success(PlantConditionResponse.From(plantCondition));
        }
    }
}
