using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Errors;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition
{
    internal sealed class CreatePlantConditionHandler(
        IPlantConditionRepository plantConditionRepository,
        IPlantsUnitOfWork plantUnitOfWork,
        TimeProvider timeProvider) : IRequestHandler<CreatePlantConditionCommand, Result<PlantConditionResponse>>
    {
        private static readonly string[] CodeConstraints =
        [
            "uq_plant_conditions_active_code",
            "uq_plant_conditions_code_revision"
        ];

        public async Task<Result<PlantConditionResponse>> Handle(CreatePlantConditionCommand request, CancellationToken cancellationToken)
        {
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

            return Result.Success(new PlantConditionResponse(
                plantCondition.Id,
                plantCondition.Code,
                plantCondition.Name,
                plantCondition.ScientificName,
                plantCondition.ConditionType,
                plantCondition.Description,
                plantCondition.RevisionNumber,
                plantCondition.IsActive,
                plantCondition.CreatedAt,
                plantCondition.RetiredAt,
                plantCondition.Version));
        }
    }
}
