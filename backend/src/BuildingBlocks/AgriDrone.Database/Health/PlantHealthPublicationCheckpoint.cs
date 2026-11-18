using AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult;

namespace AgriDrone.Database.Health;

internal sealed class PlantHealthPublicationCheckpoint : IPlantHealthPublicationCheckpoint
{
    public Task AfterPlantStagedAsync(Guid publicationId, Guid plantId, int stagedCount, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
