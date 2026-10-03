package com.agridrone.be1.shared.audit;

import com.agridrone.be1.shared.execution.ExecutionContextSnapshot;
import java.sql.Types;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class JdbcAuditRepository implements AuditRepository {
    private final JdbcTemplate jdbc;

    public JdbcAuditRepository(JdbcTemplate jdbc) {
        this.jdbc = jdbc;
    }

    @Override
    public void append(ExecutionContextSnapshot context, AuditEntry entry, String beforeJson, String afterJson) {
        jdbc.update(connection -> {
            var statement = connection.prepareStatement("""
                    INSERT INTO audit.audit_logs(
                        user_id, tenant_id, farm_id, actor_type, actor_id, correlation_id,
                        entity_type, entity_id, action, old_data, new_data, reason)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?::jsonb, ?::jsonb, ?)
                    """);
            statement.setObject(1, context.actorId(), Types.OTHER);
            statement.setObject(
                    2,
                    entry.tenantId() == null ? context.tenantId() : entry.tenantId(),
                    Types.OTHER);
            statement.setObject(3, entry.farmId(), Types.OTHER);
            statement.setString(4, context.actorType().name());
            statement.setObject(5, context.actorId(), Types.OTHER);
            statement.setObject(6, context.correlationId(), Types.OTHER);
            statement.setString(7, entry.entityType());
            statement.setObject(8, entry.entityId(), Types.OTHER);
            statement.setString(9, entry.action());
            statement.setString(10, beforeJson);
            statement.setString(11, afterJson);
            statement.setString(12, entry.reason());
            return statement;
        });
    }
}
