using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class PlantHealthReviewOperation : Entity
{
    private PlantHealthReviewOperation() { }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid SurveyResultId { get; private set; }
    public Guid BatchId { get; private set; }
    public string Fingerprint { get; private set; } = null!;
    public JsonDocument Outcome { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public static PlantHealthReviewOperation Create(Guid tenantId, Guid actorId, Guid operationId,
        Guid resultId, Guid batchId, string fingerprint, JsonDocument outcome, DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(tenantId); DomainGuard.NotEmpty(actorId); DomainGuard.NotEmpty(operationId);
        DomainGuard.NotEmpty(resultId); DomainGuard.NotEmpty(batchId); DomainGuard.Utc(createdAt);
        ArgumentNullException.ThrowIfNull(outcome);
        if (fingerprint.Length != 64) throw new ArgumentException("Expected a SHA-256 fingerprint.", nameof(fingerprint));
        return new() { Id = Guid.CreateVersion7(), TenantId = tenantId, ActorId = actorId, OperationId = operationId,
            SurveyResultId = resultId, BatchId = batchId, Fingerprint = fingerprint,
            Outcome = JsonDocument.Parse(outcome.RootElement.GetRawText()), CreatedAt = createdAt };
    }
}
