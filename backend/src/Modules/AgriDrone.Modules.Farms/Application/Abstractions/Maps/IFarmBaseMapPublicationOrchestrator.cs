using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Maps;

public sealed record FarmBaseMapPublishedResult(
    Guid FarmBaseMapVersionId,
    int VersionNumber,
    int ConfirmedSurveyPoleCount,
    DateTimeOffset PublishedAt);

public interface IFarmBaseMapPublicationOrchestrator
{
    Task<Result<FarmBaseMapPublishedResult>> PublishAsync(
        Guid farmId,
        Guid draftId,
        Guid actorId,
        CancellationToken cancellationToken);
}
