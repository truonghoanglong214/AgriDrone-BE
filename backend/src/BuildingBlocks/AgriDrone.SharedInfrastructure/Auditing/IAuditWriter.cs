using System.Text.Json;

namespace AgriDrone.SharedInfrastructure.Auditing;

public interface IAuditWriter
{
    void AddUserAction(
        IAuditLogSink sink,
        Guid tenantId,
        Guid? farmId,
        Guid actorId,
        Guid correlationId,
        string entityType,
        Guid entityId,
        string action,
        JsonDocument? oldData,
        JsonDocument? newData,
        DateTimeOffset createdAt);

    void AddSystemAdminAction(
        IAuditLogSink sink,
        Guid actorId,
        Guid correlationId,
        string entityType,
        Guid entityId,
        string action,
        JsonDocument? oldData,
        JsonDocument? newData,
        DateTimeOffset createdAt);
}
