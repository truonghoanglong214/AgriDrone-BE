using System.Text.Json;

namespace AgriDrone.SharedInfrastructure.Auditing;

internal sealed class AuditWriter : IAuditWriter
{
    public void AddUserAction(
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
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.AddAuditLog(
            AuditLog.ForUserAction(
                tenantId,
                farmId,
                actorId,
                correlationId,
                entityType,
                entityId,
                action,
                oldData,
                newData,
                createdAt));
    }

    public void AddSystemAdminAction(
        IAuditLogSink sink,
        Guid actorId,
        Guid correlationId,
        string entityType,
        Guid entityId,
        string action,
        JsonDocument? oldData,
        JsonDocument? newData,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.AddAuditLog(
            AuditLog.ForSystemAdminAction(
                actorId,
                correlationId,
                entityType,
                entityId,
                action,
                oldData,
                newData,
                createdAt));
    }
}
