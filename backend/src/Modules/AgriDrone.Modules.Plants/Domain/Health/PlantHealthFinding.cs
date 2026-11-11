using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Plants.Domain.Health;

/// <summary>Immutable AI source revision. Human decisions belong to a separate review history.</summary>
public sealed class PlantHealthFinding : AggregateRoot
{
    private PlantHealthFinding() { }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid SourceFindingId { get; private set; }
    public int SourceVersion { get; private set; }
    public Guid? PlantId { get; private set; }
    public Guid? MappingCandidateId { get; private set; }
    public Guid? SupersedesFindingId { get; private set; }
    public Point OriginalPosition { get; private set; } = null!;
    public string? SuspectedClassCode { get; private set; }
    public string ResolutionState { get; private set; } = null!;
    public string BoundaryClassification { get; private set; } = null!;
    public string Fingerprint { get; private set; } = null!;
    public JsonDocument Original { get; private set; } = null!;
    public DateTimeOffset ObservedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public uint Version { get; private set; }

    public static PlantHealthFinding Create(Guid tenantId, Guid farmId, Guid resultId, Guid missionId,
        PlantHealthFindingV4 source, string? suspectedClassCode, string boundaryClassification,
        Guid? supersedesFindingId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        DomainGuard.NotEmpty(tenantId); DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(resultId); DomainGuard.NotEmpty(missionId); DomainGuard.Utc(createdAt);
        return new PlantHealthFinding
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, FarmId = farmId,
            SurveyResultId = resultId, MissionId = missionId,
            SourceFindingId = source.FindingId, SourceVersion = source.FindingVersion,
            PlantId = source.PlantId, MappingCandidateId = source.MappingCandidateId,
            SupersedesFindingId = supersedesFindingId,
            OriginalPosition = new Point(source.Position.Coordinates[0], source.Position.Coordinates[1]) { SRID = 4326 },
            SuspectedClassCode = suspectedClassCode, BoundaryClassification = boundaryClassification,
            ResolutionState = suspectedClassCode is null || source.PlantId is null ||
                boundaryClassification != "IN_BOUNDARY" || source.Uncertainty != "NONE" ? "NEEDS_REVIEW" : "PENDING_REVIEW",
            Fingerprint = PlantHealthV4Fingerprint.ForFinding(source),
            Original = JsonSerializer.SerializeToDocument(source), ObservedAt = source.ObservedAt, CreatedAt = createdAt
        };
    }
}

/// <summary>Many import batches may refer to the same canonical source revision.</summary>
public sealed class PlantHealthBatchFinding
{
    private PlantHealthBatchFinding() { }
    public Guid BatchId { get; private set; }
    public Guid FindingId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public static PlantHealthBatchFinding Create(Guid batchId, PlantHealthFinding finding) => new()
    {
        BatchId = batchId, FindingId = finding.Id, TenantId = finding.TenantId,
        FarmId = finding.FarmId, SurveyResultId = finding.SurveyResultId
    };
}
