using System.Text.Json.Serialization;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishPlantHealthSurveyResultRequest(Guid OperationId, Guid BatchId,
    uint ExpectedResultVersion, long ExpectedReviewRevision, string ApprovalFingerprint);

public sealed record PublishPlantHealthSurveyResultResponse(Guid OperationId, Guid PublicationId,
    Guid ResultId, Guid BatchId, long PublicationVersion, string Status, string OrderStatus,
    string ResultNature, string TeamLabelLimitation, string Conclusion,
    int PublishedFindingCount, int PublishedZoneCount, IReadOnlyList<Guid> AdvancedPlantIds,
    Guid PublishedBy, DateTimeOffset PublishedAt, string PublicationFingerprint, bool IsReplay = false);

public interface IPlantHealthResultPublisher
{
    Task<Result<PublishPlantHealthSurveyResultResponse>> PublishResultAsync(Guid orderId,
        PublishPlantHealthSurveyResultRequest request, CancellationToken token);
}

/// <summary>Replaceable failure seam for final-phase rollback verification; production implementation does nothing.</summary>
public interface IPlantHealthPublicationCheckpoint
{
    Task AfterPlantStagedAsync(Guid publicationId, Guid plantId, int stagedCount, CancellationToken token);
}
