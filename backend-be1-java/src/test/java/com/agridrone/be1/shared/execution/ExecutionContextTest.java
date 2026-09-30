package com.agridrone.be1.shared.execution;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.util.Set;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class ExecutionContextTest {
    @Test
    void scopeIsImmutableAndAlwaysCleared() {
        var snapshot = new ExecutionContextSnapshot(null, null, ActorType.SYSTEM,
                UUID.randomUUID(), null, ExecutionSource.SYSTEM, Set.of("SYSTEM_ADMIN"));
        try (var ignored = ExecutionContext.begin(snapshot)) {
            assertThat(ExecutionContext.require()).isEqualTo(snapshot);
            assertThatThrownBy(() -> ExecutionContext.begin(snapshot)).isInstanceOf(IllegalStateException.class);
        }
        assertThat(ExecutionContext.current()).isEmpty();
    }

    @Test
    void rabbitContextRequiresTenantAndMessageIdentity() {
        assertThatThrownBy(() -> new ExecutionContextSnapshot(null, null, ActorType.SYSTEM,
                UUID.randomUUID(), null, ExecutionSource.RABBIT_MQ, Set.of()))
                .isInstanceOf(IllegalArgumentException.class);
    }
}
