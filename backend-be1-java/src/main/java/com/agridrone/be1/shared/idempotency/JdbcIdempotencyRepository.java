package com.agridrone.be1.shared.idempotency;

import java.sql.Timestamp;
import java.time.Instant;
import java.util.Optional;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Repository;

@Repository
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class JdbcIdempotencyRepository implements IdempotencyRepository {
    private final JdbcTemplate jdbc;

    public JdbcIdempotencyRepository(JdbcTemplate jdbc) {
        this.jdbc = jdbc;
    }

    @Override
    public boolean tryAcquire(String scope, String key, String requestHash, Instant expiresAt) {
        return jdbc.update("""
                INSERT INTO messaging.idempotency_records(scope, idempotency_key, request_hash, expires_at)
                VALUES (?, ?, ?, ?) ON CONFLICT DO NOTHING
                """, scope, key, requestHash, Timestamp.from(expiresAt)) == 1;
    }

    @Override
    public Optional<IdempotencyRecord> find(String scope, String key) {
        return jdbc.query("""
                SELECT request_hash, status, response_status, response_body::text
                FROM messaging.idempotency_records WHERE scope=? AND idempotency_key=?
                """, (rs, row) -> new IdempotencyRecord(rs.getString(1), rs.getString(2),
                (Integer) rs.getObject(3), rs.getString(4)), scope, key).stream().findFirst();
    }

    @Override
    public void complete(String scope, String key, int responseStatus, String responseBodyJson) {
        int changed = jdbc.update("""
                UPDATE messaging.idempotency_records SET status='COMPLETED', response_status=?,
                    response_body=?::jsonb, completed_at=now()
                WHERE scope=? AND idempotency_key=? AND status='PROCESSING'
                """, responseStatus, responseBodyJson, scope, key);
        if (changed != 1) {
            throw new IllegalStateException("Idempotency record was not acquired or already completed");
        }
    }
}
