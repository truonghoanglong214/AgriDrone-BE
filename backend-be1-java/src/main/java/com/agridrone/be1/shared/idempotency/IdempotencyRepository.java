package com.agridrone.be1.shared.idempotency;

import java.time.Instant;
import java.util.Optional;

public interface IdempotencyRepository {
    boolean tryAcquire(String scope, String key, String requestHash, Instant expiresAt);
    Optional<IdempotencyRecord> find(String scope, String key);
    void complete(String scope, String key, int responseStatus, String responseBodyJson);
}
