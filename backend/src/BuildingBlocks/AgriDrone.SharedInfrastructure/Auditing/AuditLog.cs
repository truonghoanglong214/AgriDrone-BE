using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.SharedInfrastructure.Auditing;

public sealed class AuditLog : Entity<long>
{
    private AuditLog()
    {
    }

    private AuditLog(
        Guid? tenantId,
        Guid? farmId,
        Guid actorId,
        Guid correlationId,
        string entityType,
        Guid entityId,
        string action,
        JsonDocument? oldData,
        JsonDocument? newData,
        DateTimeOffset createdAt)
    {
        UserId = actorId;
        TenantId = tenantId;
        FarmId = farmId;
        ActorType = AuditActorType.User;
        ActorId = actorId;
        CorrelationId = correlationId;
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        OldData = Clone(oldData);
        NewData = Clone(newData);
        CreatedAt = createdAt;
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

    public static AuditLog ForUserAction(
        Guid tenantId,
        Guid? farmId,
        Guid actorId,
        Guid correlationId,
        string entityType,
        Guid entityId,
        string action,
        JsonDocument? oldData,
        JsonDocument? newData,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(correlationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(entityId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        if (farmId == Guid.Empty)
        {
            throw new ArgumentException(
                "FarmId cannot be empty when provided.",
                nameof(farmId));
        }

        if (createdAt == default || createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "CreatedAt must be a non-default UTC timestamp.",
                nameof(createdAt));
        }

        return new AuditLog(
            tenantId,
            farmId,
            actorId,
            correlationId,
            entityType.Trim(),
            entityId,
            action.Trim(),
            oldData,
            newData,
            createdAt);
    }

    public static AuditLog ForSystemAdminAction(
        Guid actorId,
        Guid correlationId,
        string entityType,
        Guid entityId,
        string action,
        JsonDocument? oldData,
        JsonDocument? newData,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(correlationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(entityId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        if (createdAt == default || createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "CreatedAt must be a non-default UTC timestamp.",
                nameof(createdAt));
        }

        return new AuditLog(
            tenantId: null,
            farmId: null,
            actorId,
            correlationId,
            entityType.Trim(),
            entityId,
            action.Trim(),
            oldData,
            newData,
            createdAt);
    }

    private static JsonDocument? Clone(JsonDocument? document) =>
        document is null
            ? null
            : JsonDocument.Parse(document.RootElement.GetRawText());
}
