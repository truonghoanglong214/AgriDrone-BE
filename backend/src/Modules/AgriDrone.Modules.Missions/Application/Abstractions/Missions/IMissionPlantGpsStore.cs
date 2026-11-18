namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public interface IMissionPlantGpsStore
{
    Task<IReadOnlyList<MissionPlantGpsRow>?> GetAsync(Guid tenantId, Guid farmId,
        Guid missionId, CancellationToken cancellationToken);

    Task<MissionPlantGpsReplaceResult> ReplaceAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        IReadOnlyList<MissionPlantGpsRow> rows,
        MappingProcessingCompletion completion,
        CancellationToken cancellationToken);
}

public sealed record MissionPlantGpsRow(string TreeId, int Row, int Col,
    decimal Latitude, decimal Longitude, decimal Confidence, string Status,
    string Source);

public sealed record MappingProcessingCompletion(
    Guid ClientOperationId,
    Guid ModelVersionId,
    int SourceVersion,
    double GridBearingDeg,
    double RowSpacingM,
    double PlantSpacingM,
    string AlgorithmVersion,
    string InputFingerprint,
    string InputSource,
    DateTimeOffset StartedAt);

public enum MissionPlantGpsReplaceStatus
{
    Saved,
    NotFound,
    Conflict,
    Invalid
}

public sealed record MissionPlantGpsReplaceResult(
    MissionPlantGpsReplaceStatus Status,
    Guid? ProcessingJobId = null,
    IReadOnlyList<string>? Errors = null);
