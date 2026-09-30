package com.agridrone.be1.shared.cache;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.fasterxml.jackson.databind.ObjectMapper;
import java.time.Duration;
import java.util.concurrent.atomic.AtomicInteger;
import org.junit.jupiter.api.Test;
import org.springframework.data.redis.RedisConnectionFailureException;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ValueOperations;

class VersionedJsonCacheTest {
    @Test
    void redisOutageFallsBackToAuthoritativeStoreAndKeyIsServiceScoped() {
        StringRedisTemplate redis = mock(StringRedisTemplate.class);
        @SuppressWarnings("unchecked") ValueOperations<String, String> values = mock(ValueOperations.class);
        when(redis.opsForValue()).thenReturn(values);
        when(values.get("agridrone:be1:v1:tenant:42"))
                .thenThrow(new RedisConnectionFailureException("redis stopped"));
        var cache = new VersionedJsonCache(redis, new ObjectMapper(),
                new CacheProperties(true, "agridrone:be1", "v1", Duration.ofMinutes(5), Duration.ofSeconds(2)));
        AtomicInteger loads = new AtomicInteger();

        String result = cache.getOrLoad("tenant", "42", String.class, () -> {
            loads.incrementAndGet();
            return "from-postgres";
        });

        assertThat(result).isEqualTo("from-postgres");
        assertThat(loads).hasValue(1);
        assertThat(cache.key("tenant", "42")).isEqualTo("agridrone:be1:v1:tenant:42");
    }
}
