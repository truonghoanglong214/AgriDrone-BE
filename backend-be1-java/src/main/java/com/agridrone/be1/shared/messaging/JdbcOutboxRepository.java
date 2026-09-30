package com.agridrone.be1.shared.messaging;

import java.sql.Timestamp;
import java.sql.Types;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.support.TransactionTemplate;

@Repository
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class JdbcOutboxRepository implements OutboxRepository {
    private final JdbcTemplate jdbc;
    private final TransactionTemplate transactions;

    public JdbcOutboxRepository(JdbcTemplate jdbc, TransactionTemplate transactions) {
        this.jdbc = jdbc;
        this.transactions = transactions;
    }

    @Override
    public void enqueue(IntegrationEventEnvelope event, String routingKey, String partitionKey, byte[] body) {
        jdbc.update(connection -> {
            var statement = connection.prepareStatement("""
                    INSERT INTO messaging.outbox_messages(
                        message_id, tenant_id, correlation_id, actor_id, event_type, schema_version,
                        routing_key, body, content_type, partition_key, status, next_attempt_at,
                        occurred_at, created_at)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, 'application/json', ?, 'PENDING', now(), ?, now())
                    """);
            statement.setObject(1, event.messageId(), Types.OTHER);
            statement.setObject(2, event.tenantId(), Types.OTHER);
            statement.setObject(3, event.correlationId(), Types.OTHER);
            statement.setObject(4, event.actorId(), Types.OTHER);
            statement.setString(5, event.eventType());
            statement.setInt(6, event.schemaVersion());
            statement.setString(7, routingKey);
            statement.setBytes(8, body);
            statement.setString(9, partitionKey);
            statement.setObject(10, event.occurredAt());
            return statement;
        });
    }

    @Override
    public List<OutboxMessage> claim(UUID workerId, int batchSize, Duration lease) {
        return transactions.execute(status -> jdbc.query("""
                WITH candidates AS (
                    SELECT message_id FROM messaging.outbox_messages
                    WHERE (status IN ('PENDING','RETRY') AND next_attempt_at <= now())
                       OR (status = 'PROCESSING' AND locked_until < now())
                    ORDER BY occurred_at, message_id
                    LIMIT ? FOR UPDATE SKIP LOCKED
                )
                UPDATE messaging.outbox_messages o
                SET status='PROCESSING', attempt_count=attempt_count+1,
                    locked_by=?, locked_until=now() + (? * interval '1 millisecond'),
                    next_attempt_at=NULL, version=version+1
                FROM candidates c WHERE o.message_id=c.message_id
                RETURNING o.message_id, o.routing_key, o.body, o.attempt_count
                """, (rs, row) -> new OutboxMessage(rs.getObject("message_id", UUID.class),
                        rs.getString("routing_key"), rs.getBytes("body"), rs.getInt("attempt_count")),
                batchSize, workerId, lease.toMillis()));
    }

    @Override
    public void markPublished(UUID messageId, UUID workerId) {
        requireSingle(jdbc.update("""
                UPDATE messaging.outbox_messages SET status='PUBLISHED', published_at=now(),
                    locked_by=NULL, locked_until=NULL, last_error=NULL, version=version+1
                WHERE message_id=? AND status='PROCESSING' AND locked_by=?
                """, messageId, workerId));
    }

    @Override
    public void markFailed(OutboxMessage message, UUID workerId, Instant nextAttempt, int maxAttempts, String error) {
        boolean dead = message.attemptCount() >= maxAttempts;
        requireSingle(jdbc.update("""
                UPDATE messaging.outbox_messages SET status=?, next_attempt_at=?,
                    locked_by=NULL, locked_until=NULL, last_error=?, version=version+1
                WHERE message_id=? AND status='PROCESSING' AND locked_by=?
                """, dead ? "DEAD" : "RETRY", dead ? null : Timestamp.from(nextAttempt),
                truncate(error), message.messageId(), workerId));
    }

    @Override
    public void redrive(UUID messageId) {
        requireSingle(jdbc.update("""
                UPDATE messaging.outbox_messages SET status='RETRY', attempt_count=0,
                    next_attempt_at=now(), locked_by=NULL, locked_until=NULL,
                    published_at=NULL, last_error=NULL, version=version+1
                WHERE message_id=? AND status='DEAD'
                """, messageId));
    }

    private static String truncate(String value) {
        if (value == null) return "Unknown publish failure";
        return value.length() <= 2000 ? value : value.substring(0, 2000);
    }

    private static void requireSingle(int changed) {
        if (changed != 1) throw new IllegalStateException("Outbox row changed concurrently or was not found");
    }
}
