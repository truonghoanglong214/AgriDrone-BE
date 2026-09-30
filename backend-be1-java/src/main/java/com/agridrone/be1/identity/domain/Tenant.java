package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

public final class Tenant {
    private final UUID id;
    private final String code;
    private final String name;
    private TenantStatus status;
    private final Instant createdAt;
    private Instant updatedAt;
    private final Instant deletedAt;

    public Tenant(
            UUID id,
            String code,
            String name,
            TenantStatus status,
            Instant createdAt,
            Instant updatedAt,
            Instant deletedAt) {
        this.id = Objects.requireNonNull(id);
        this.code = required(code, "code");
        this.name = required(name, "name");
        this.status = Objects.requireNonNull(status);
        this.createdAt = Objects.requireNonNull(createdAt);
        this.updatedAt = Objects.requireNonNull(updatedAt);
        this.deletedAt = deletedAt;
    }

    public static Tenant create(String code, String name, Instant now) {
        return new Tenant(UUID.randomUUID(), code, name, TenantStatus.ACTIVE, now, now, null);
    }
    public void activate(Instant now) {
        status = TenantStatus.ACTIVE;
        updatedAt = now;
    }

    public void deactivate(Instant now) {
        status = TenantStatus.INACTIVE;
        updatedAt = now;
    }

    public boolean isActive() {
        return status == TenantStatus.ACTIVE && deletedAt == null;
    }

    public UUID id() {
        return id;
    }

    public String code() {
        return code;
    }

    public String name() {
        return name;
    }

    public TenantStatus status() {
        return status;
    }

    public Instant createdAt() {
        return createdAt;
    }

    public Instant updatedAt() {
        return updatedAt;
    }

    public Instant deletedAt() {
        return deletedAt;
    }

    private static String required(String value, String name) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(name + " is required");
        }
        return value.trim();
    }
}
