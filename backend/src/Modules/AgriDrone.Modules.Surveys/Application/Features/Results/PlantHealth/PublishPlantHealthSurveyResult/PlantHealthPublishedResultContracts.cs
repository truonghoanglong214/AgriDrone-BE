using AgriDrone.IntegrationContracts.Health;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult;

public sealed record PlantHealthPublishedResultResponse(PlantHealthPreliminaryResultPublishedV1 Publication, string Fingerprint);

public interface IPlantHealthPublishedResultReader
{
    Task<Result<PlantHealthPublishedResultResponse>> GetPublishedAsync(Guid orderId, CancellationToken token);
}
