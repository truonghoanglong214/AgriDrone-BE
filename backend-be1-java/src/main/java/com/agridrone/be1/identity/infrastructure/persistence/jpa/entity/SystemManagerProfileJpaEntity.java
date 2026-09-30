package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import com.agridrone.be1.identity.domain.ManagerAvailability;
import com.agridrone.be1.identity.domain.QualificationStatus;
import com.agridrone.be1.identity.domain.SystemManagerProfile;
import com.agridrone.be1.identity.domain.SystemManagerProfileStatus;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Version;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "system_manager_profiles", schema = "identity")
public class SystemManagerProfileJpaEntity {
    @Id
    private UUID id;

    @Column(name = "user_id", nullable = false)
    private UUID userId;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private SystemManagerProfileStatus status;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private ManagerAvailability availability;

    @Enumerated(EnumType.STRING)
    @Column(name = "qualification_status", nullable = false)
    private QualificationStatus qualificationStatus;

    @Column(name = "qualification_expires_at")
    private Instant qualificationExpiresAt;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @Version
    @Column(nullable = false)
    private long version;

    protected SystemManagerProfileJpaEntity() {}

    public SystemManagerProfileJpaEntity(SystemManagerProfile profile) {
        id = profile.id();
        userId = profile.userId();
        createdAt = profile.createdAt();
        version = profile.version();
        updateFields(profile);
    }

    public void applyMutation(SystemManagerProfile profile) {
        if (profile.version() != version + 1) {
            throw new IllegalStateException("System manager profile version conflict");
        }
        updateFields(profile);
    }

    private void updateFields(SystemManagerProfile profile) {
        status = profile.status();
        availability = profile.availability();
        qualificationStatus = profile.qualificationStatus();
        qualificationExpiresAt = profile.qualificationExpiresAt();
        updatedAt = profile.updatedAt();
    }

    public SystemManagerProfile toDomain() {
        return new SystemManagerProfile(
                id,
                userId,
                status,
                availability,
                qualificationStatus,
                qualificationExpiresAt,
                createdAt,
                updatedAt,
                version);
    }
}
