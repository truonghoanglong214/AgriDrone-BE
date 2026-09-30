package com.agridrone.be1.shared.execution;

import java.util.Set;
import java.util.UUID;

public record ExecutionContextSnapshot(
        UUID tenantId,
        UUID actorId,
        ActorType actorType,
        UUID correlationId,
        UUID messageId,
        ExecutionSource source,
        Set<String> roles) {

    public ExecutionContextSnapshot {
        actorType = actorType == null ? ActorType.SYSTEM : actorType;
        source = source == null ? ExecutionSource.SYSTEM : source;
        roles = roles == null ? Set.of() : Set.copyOf(roles);
        if (correlationId == null) {
            throw new IllegalArgumentException("correlationId is required");
        }
        if (actorType == ActorType.USER && actorId == null) {
            throw new IllegalArgumentException("actorId is required for USER actors");
        }
        if (source == ExecutionSource.RABBIT_MQ && (tenantId == null || messageId == null)) {
            throw new IllegalArgumentException("RabbitMQ execution requires tenantId and messageId");
        }
    }
}
