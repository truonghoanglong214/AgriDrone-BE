namespace AgriDrone.SharedInfrastructure.Auditing;

public interface IAuditLogSink
{
    void AddAuditLog(AuditLog auditLog);
}
