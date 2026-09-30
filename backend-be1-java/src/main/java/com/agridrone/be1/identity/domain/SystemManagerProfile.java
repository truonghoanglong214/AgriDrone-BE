package com.agridrone.be1.identity.domain;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

public final class SystemManagerProfile {
    private final UUID id;
    private final UUID userId;
    private SystemManagerProfileStatus status;
    private ManagerAvailability availability;
    private QualificationStatus qualificationStatus;
    private Instant qualificationExpiresAt;
    private final Instant createdAt;
    private Instant updatedAt;
    private long version;

    public SystemManagerProfile(
            UUID id,
            UUID userId,
            SystemManagerProfileStatus status,
            ManagerAvailability availability, QualificationStatus qualificationStatus,
            Instant qualificationExpiresAt, Instant createdAt, Instant updatedAt, long version) {
        this.id = Objects.requireNonNull(id);
        this.userId = Objects.requireNonNull(userId);
        this.status = Objects.requireNonNull(status);
        this.availability = Objects.requireNonNull(availability);
        this.qualificationStatus = Objects.requireNonNull(qualificationStatus);
        this.qualificationExpiresAt = qualificationExpiresAt;
        this.createdAt = Objects.requireNonNull(createdAt);
        this.updatedAt = Objects.requireNonNull(updatedAt);
        this.version = version;
        if (version < 1) {
            throw new IllegalArgumentException("version must be positive");
        }
    }

    public static SystemManagerProfile create(UUID userId, Instant now) {
        return new SystemManagerProfile(
                UUID.randomUUID(),
                userId,
                SystemManagerProfileStatus.SUSPENDED,
                ManagerAvailability.UNAVAILABLE,
                QualificationStatus.PENDING,
                null,
                now,
                now,
                1);
    }

    public void activate(Instant now, long expectedVersion) {
        checkVersion(expectedVersion);
        status = SystemManagerProfileStatus.ACTIVE;
        touch(now);
    }

    public void suspend(Instant now, long expectedVersion) {
        checkVersion(expectedVersion);
        status = SystemManagerProfileStatus.SUSPENDED;
        availability = ManagerAvailability.UNAVAILABLE;
        touch(now);
    }

    public void updateAvailability(
            ManagerAvailability value,
            Instant now,
            long expectedVersion) {
        checkVersion(expectedVersion);
        availability = Objects.requireNonNull(value);
        touch(now);
    }

    public void updateQualification(
            QualificationStatus value,
            Instant expiresAt,
            Instant now,
            long expectedVersion) {
        checkVersion(expectedVersion);
        if (value == QualificationStatus.QUALIFIED
                && (expiresAt == null || !expiresAt.isAfter(now))) {
            throw new IllegalArgumentException(
                    "A qualified manager requires a future qualification expiry");
        }
        qualificationStatus = Objects.requireNonNull(value);
        qualificationExpiresAt = expiresAt;
        if (value != QualificationStatus.QUALIFIED) {
            availability = ManagerAvailability.UNAVAILABLE;
        }
        touch(now);
    }

    public boolean canBeAssigned(Instant now) {
        return status == SystemManagerProfileStatus.ACTIVE
                && availability == ManagerAvailability.AVAILABLE
                && hasQualification(now);
    }

    public boolean canOperate(Instant now) {
        return status == SystemManagerProfileStatus.ACTIVE && hasQualification(now);
    }

    private boolean hasQualification(Instant now) {
        return qualificationStatus == QualificationStatus.QUALIFIED
                && qualificationExpiresAt != null
                && qualificationExpiresAt.isAfter(now);
    }

    private void checkVersion(long expected) {
        if (expected != version) {
            throw new IllegalStateException("System manager profile version conflict");
        }
    }

    private void touch(Instant now) {
        updatedAt = Objects.requireNonNull(now);
        version++;
    }

    public UUID id() {
        return id;
    }

    public UUID userId() {
        return userId;
    }

    public SystemManagerProfileStatus status() {
        return status;
    }

    public ManagerAvailability availability() {
        return availability;
    }

    public QualificationStatus qualificationStatus() {
        return qualificationStatus;
    }

    public Instant qualificationExpiresAt() {
        return qualificationExpiresAt;
    }

    public Instant createdAt() {
        return createdAt;
    }

    public Instant updatedAt() {
        return updatedAt;
    }

    public long version() {
        return version;
    }
}
