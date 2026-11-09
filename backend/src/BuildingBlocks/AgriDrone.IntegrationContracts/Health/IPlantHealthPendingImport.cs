using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.IntegrationContracts.Health;

/// <summary>Stages pending output inside the caller-owned Inbox transaction.</summary>
public interface IPlantHealthPendingImport
{
    Task<PlantHealthImportOutcome> ImportAsync(
        IntegrationEventEnvelope<PlantHealthAnalysisReadyV4> envelope,
        CancellationToken cancellationToken);
}

public enum PlantHealthImportDisposition { Completed, Retry, Rejected }

public sealed record PlantHealthImportOutcome(
    PlantHealthImportDisposition Disposition,
    Guid? ResultId = null,
    Guid? BatchId = null,
    bool Idempotent = false,
    int BlockingCount = 0,
    string? ErrorCode = null,
    string? Error = null);

/// <summary>Immutable capture/job context supplied by Missions, without domain entities.</summary>
public interface IPlantHealthSourceContextQuery
{
    Task<PlantHealthSourceContext?> GetAsync(Guid missionId, Guid jobId, CancellationToken cancellationToken);
}

public sealed record PlantHealthSourceContext(
    Guid TenantId, Guid FarmId, Guid? SurveyOrderId, bool IsPlantHealth,
    Guid? FarmBoundaryVersionId, Guid? FarmBaseMapVersionId,
    int SourceVersion, bool Completed, PlantHealthProvenanceV4? Provenance,
    string? TeamLabelLimitation, IReadOnlyList<Guid> EvidenceMediaIds,
    IReadOnlyList<Guid>? InputMediaIds = null, int? ProcessedImageCount = null);

public static class PlantHealthV4Values
{
    public const string ResultNature = "PRELIMINARY_SUSPECTED";
    public const string ClassificationNature = "SUSPECTED";
    public const decimal BoundaryReviewThresholdMeters = 1m;
}
