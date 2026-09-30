package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

public final class FarmManagerAssignment {
    private final UUID id;
    private final UUID tenantId;
    private final UUID farmId;
    private final UUID profileId;
    private final UUID assignedBy;
    private final String assignmentReason;
    private final Instant assignedAt;
    private UUID endedBy;
    private String endReason;
    private Instant endedAt;
    private long version;

    public FarmManagerAssignment(
            UUID id,
            UUID tenantId,
            UUID farmId,
            UUID profileId,
            UUID assignedBy,
            String assignmentReason,
            Instant assignedAt,
            UUID endedBy,
            String endReason,
            Instant endedAt,
            long version) {
        this.id = Objects.requireNonNull(id);
        this.tenantId = Objects.requireNonNull(tenantId);
        this.farmId = Objects.requireNonNull(farmId);
        this.profileId = Objects.requireNonNull(profileId);
        this.assignedBy = Objects.requireNonNull(assignedBy);
        if (assignmentReason == null || assignmentReason.isBlank()) {
            throw new IllegalArgumentException("assignmentReason is required");
        }
        this.assignmentReason = assignmentReason.trim();
        this.assignedAt = Objects.requireNonNull(assignedAt);
        this.endedBy = endedBy;
        this.endReason = endReason;
        this.endedAt = endedAt;
        this.version = version;
    }

    public static FarmManagerAssignment create(
            UUID tenantId,
            UUID farmId,
            UUID profileId,
            UUID assignedBy,
            String reason,
            Instant now) {
        return new FarmManagerAssignment(
                UUID.randomUUID(), tenantId, farmId, profileId, assignedBy,
                reason, now, null, null, null, 1);
    }

    public void end(UUID actorId, String reason, Instant now, long expectedVersion) {
        if (version != expectedVersion) {
            throw new IllegalStateException("Farm manager assignment version conflict");
        }
        if (!isActive()) {
            throw new IllegalStateException("Assignment has already ended");
        }
        if (now.isBefore(assignedAt)) {
            throw new IllegalArgumentException("endedAt cannot precede assignedAt");
        }
        if (reason == null || reason.isBlank()) {
            throw new IllegalArgumentException("end reason is required");
        }
        endedBy = Objects.requireNonNull(actorId);
        endReason = reason.trim();
        endedAt = now;
        version++;
    }

    public boolean isActive() {
        return endedAt == null;
    }

    public UUID id() {
        return id;
    }

    public UUID tenantId() {
        return tenantId;
    }

    public UUID farmId() {
        return farmId;
    }

    public UUID profileId() {
        return profileId;
    }

    public UUID assignedBy() {
        return assignedBy;
    }

    public String assignmentReason() {
        return assignmentReason;
    }

    public Instant assignedAt() {
        return assignedAt;
    }

    public UUID endedBy() {
        return endedBy;
    }

    public String endReason() {
        return endReason;
    }

    public Instant endedAt() {
        return endedAt;
    }

    public long version() {
        return version;
    }
}
