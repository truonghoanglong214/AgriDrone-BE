using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class PlantHealthImportBatch : AggregateRoot
{
    private PlantHealthImportBatch() { }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid HandoffId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public Guid FarmBaseMapVersionId { get; private set; }
    public int SourceVersion { get; private set; }
    public Guid? SupersedesBatchId { get; private set; }
    public string Fingerprint { get; private set; } = null!;
    public JsonDocument OriginalPayload { get; private set; } = null!;
    public DateTimeOffset ProducedAt { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }
    public int BlockingCount { get; private set; }
    public uint Version { get; private set; }

    public static PlantHealthImportBatch Create(Guid tenantId, Guid resultId,
        PlantHealthAnalysisReadyV4 payload, int sourceVersion, Guid? supersedesBatchId,
        int blockingCount, DateTimeOffset importedAt)
    {
        ArgumentNullException.ThrowIfNull(payload);
        DomainGuard.NotEmpty(tenantId); DomainGuard.NotEmpty(resultId); DomainGuard.Utc(importedAt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(blockingCount);
        return new PlantHealthImportBatch
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, FarmId = payload.FarmId,
            SurveyOrderId = payload.SurveyOrderId, SurveyResultId = resultId,
            MissionId = payload.MissionId, HandoffId = payload.HandoffId, JobId = payload.JobId,
            FarmBoundaryVersionId = payload.FarmBoundaryVersionId,
            FarmBaseMapVersionId = payload.FarmBaseMapVersionId,
            SourceVersion = sourceVersion, SupersedesBatchId = supersedesBatchId,
            Fingerprint = PlantHealthV4Fingerprint.ForPayload(payload),
            OriginalPayload = JsonSerializer.SerializeToDocument(payload),
            ProducedAt = payload.ProducedAt, ImportedAt = importedAt, BlockingCount = blockingCount
        };
    }
}
