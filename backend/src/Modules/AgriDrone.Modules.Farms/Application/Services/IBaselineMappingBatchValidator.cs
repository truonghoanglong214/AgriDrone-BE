using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;

namespace AgriDrone.Modules.Farms.Application.Services;

public interface IBaselineMappingBatchValidator
{
    BaselineMappingBatchValidationResult Validate(
        MappingCandidateBatch batch,
        FarmBoundary? boundary);
}
