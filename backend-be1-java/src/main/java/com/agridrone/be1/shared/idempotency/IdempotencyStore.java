package com.agridrone.be1.shared.idempotency;

import com.agridrone.be1.shared.error.StableApiException;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.time.Duration;
import java.time.Instant;
import java.util.HexFormat;
import org.springframework.http.HttpStatus;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class IdempotencyStore {
    private final IdempotencyRepository repository;
    private final ObjectMapper mapper;

    public IdempotencyStore(IdempotencyRepository repository, ObjectMapper mapper) {
        this.repository = repository;
        this.mapper = mapper;
    }

    public IdempotencyResult begin(String scope, String key, byte[] request, Duration ttl) {
        String hash = sha256(request);
        if (repository.tryAcquire(scope, key, hash, Instant.now().plus(ttl))) {
            return IdempotencyResult.acquired();
        }
        IdempotencyRecord stored = repository.find(scope, key)
                .orElseThrow(() -> new IllegalStateException("Idempotency record disappeared after key conflict"));
        if (!stored.requestHash().equals(hash)) {
            throw new StableApiException(HttpStatus.CONFLICT, "Idempotency.KeyReused",
                    "The idempotency key was already used with a different request.");
        }
        if (stored.status().equals("PROCESSING")) return IdempotencyResult.inProgress();
        try {
            return IdempotencyResult.replay(stored.responseStatus(), mapper.readTree(stored.responseBody()));
        } catch (Exception exception) {
            throw new IllegalStateException("Stored idempotency response is invalid", exception);
        }
    }

    public void complete(String scope, String key, int status, JsonNode body) {
        repository.complete(scope, key, status, body.toString());
    }

    private static String sha256(byte[] value) {
        try {
            return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256")
                    .digest(value == null ? new byte[0] : value));
        } catch (NoSuchAlgorithmException impossible) {
            throw new IllegalStateException(impossible);
        }
    }
}
