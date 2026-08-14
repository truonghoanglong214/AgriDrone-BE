using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.SharedInfrastructure.Auditing;

public sealed class AuditLog : Entity<long>
{
    private AuditLog()
    {
    }

    public Guid? UserId { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? FarmId { get; private set; }

    public AuditActorType ActorType { get; private set; }

    public Guid? ActorId { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public Guid? SourceJobId { get; private set; }

    public string EntityType { get; private set; } = null!;

    public Guid? EntityId { get; private set; }

    public string Action { get; private set; } = null!;

    public JsonDocument? OldData { get; private set; }

    public JsonDocument? NewData { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
