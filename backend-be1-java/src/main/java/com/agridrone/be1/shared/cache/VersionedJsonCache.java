package com.agridrone.be1.shared.cache;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.time.Duration;
import java.util.Optional;
import java.util.function.Supplier;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Component;

@Component
@ConditionalOnProperty(name = "agridrone.runtime.enabled", havingValue = "true", matchIfMissing = true)
public class VersionedJsonCache {
    private static final Logger LOGGER = LoggerFactory.getLogger(VersionedJsonCache.class);
    private final StringRedisTemplate redis;
    private final ObjectMapper mapper;
    private final CacheProperties properties;

    public VersionedJsonCache(StringRedisTemplate redis, ObjectMapper mapper, CacheProperties properties) {
        this.redis = redis;
        this.mapper = mapper;
        this.properties = properties;
    }

    public <T> Optional<T> get(String namespace, String id, Class<T> type) {
        if (!properties.enabled()) return Optional.empty();
        try {
            String value = redis.opsForValue().get(key(namespace, id));
            return value == null ? Optional.empty() : Optional.of(mapper.readValue(value, type));
        } catch (DataAccessException | java.io.IOException failure) {
            LOGGER.warn("Cache read failed; using authoritative store namespace={}", namespace);
            return Optional.empty();
        }
    }

    public void put(String namespace, String id, Object value) {
        put(namespace, id, value, properties.defaultTtl());
    }

    public void put(String namespace, String id, Object value, Duration ttl) {
        if (!properties.enabled()) return;
        try {
            redis.opsForValue().set(key(namespace, id), mapper.writeValueAsString(value), ttl);
        } catch (DataAccessException | java.io.IOException failure) {
            LOGGER.warn("Cache write failed; authoritative store remains valid namespace={}", namespace);
        }
    }

    public void invalidate(String namespace, String id) {
        if (!properties.enabled()) return;
        try {
            redis.delete(key(namespace, id));
        } catch (DataAccessException failure) {
            LOGGER.warn("Cache invalidation failed namespace={}", namespace);
        }
    }

    public <T> T getOrLoad(String namespace, String id, Class<T> type, Supplier<T> authoritativeLoader) {
        return get(namespace, id, type).orElseGet(() -> {
            T value = authoritativeLoader.get();
            if (value != null) put(namespace, id, value);
            return value;
        });
    }

    public String key(String namespace, String id) {
        if (namespace == null || !namespace.matches("[a-z0-9-]+") || id == null || id.isBlank()) {
            throw new IllegalArgumentException("Unsafe cache key component");
        }
        return properties.prefix() + ":" + properties.version() + ":" + namespace + ":" + id;
    }
}
