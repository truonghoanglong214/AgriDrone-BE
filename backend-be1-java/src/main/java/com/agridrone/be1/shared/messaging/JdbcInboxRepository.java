package com.agridrone.be1.shared.messaging;

import java.sql.Types;
import java.util.Optional;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class JdbcInboxRepository implements InboxRepository {
    private final JdbcTemplate jdbc;

    public JdbcInboxRepository(JdbcTemplate jdbc) {
        this.jdbc = jdbc;
    }

    @Override
    public Optional<String> findStatus(String consumerName, UUID messageId) {
        return jdbc.query("""
                SELECT status FROM messaging.inbox_messages
                WHERE consumer_name=? AND message_id=?
                """, (rs, row) -> rs.getString("status"), consumerName, messageId).stream().findFirst();
    }

    @Override
    public void start(String consumerName, IntegrationEventEnvelope event) {
        jdbc.update(connection -> {
            var statement = connection.prepareStatement("""
                    INSERT INTO messaging.inbox_messages(
                        consumer_name, message_id, tenant_id, correlation_id, event_type,
                        schema_version, status, received_at)
                    VALUES (?, ?, ?, ?, ?, ?, 'PROCESSING', now())
                    """);
            statement.setString(1, consumerName);
            statement.setObject(2, event.messageId(), Types.OTHER);
            statement.setObject(3, event.tenantId(), Types.OTHER);
            statement.setObject(4, event.correlationId(), Types.OTHER);
            statement.setString(5, event.eventType());
            statement.setInt(6, event.schemaVersion());
            return statement;
        });
    }

    @Override
    public void complete(String consumerName, UUID messageId) {
        requireSingle(jdbc.update("""
                UPDATE messaging.inbox_messages SET status='COMPLETED', completed_at=now()
                WHERE consumer_name=? AND message_id=? AND status='PROCESSING'
                """, consumerName, messageId));
    }

    @Override
    public void fail(String consumerName, UUID messageId, String errorCode, String errorMessage) {
        requireSingle(jdbc.update("""
                UPDATE messaging.inbox_messages SET status='FAILED', completed_at=now(),
                    error_code=?, last_error=?
                WHERE consumer_name=? AND message_id=? AND status='PROCESSING'
                """, errorCode, truncate(errorMessage), consumerName, messageId));
    }

    private static String truncate(String value) {
        if (value == null) return "Permanent message failure";
        return value.length() <= 2000 ? value : value.substring(0, 2000);
    }

    private static void requireSingle(int changed) {
        if (changed != 1) throw new IllegalStateException("Inbox row changed concurrently or was not found");
    }
}
