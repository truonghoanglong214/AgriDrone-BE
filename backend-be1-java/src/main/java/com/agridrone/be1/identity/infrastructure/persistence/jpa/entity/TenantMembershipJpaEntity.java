package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import com.agridrone.be1.identity.domain.TenantMembership;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Version;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "tenant_memberships", schema = "identity")
public class TenantMembershipJpaEntity {
    @Id
    private UUID id;

    @Column(name = "tenant_id", nullable = false)
    private UUID tenantId;

    @Column(name = "user_id", nullable = false)
    private UUID userId;

    @Column(nullable = false)
    private String role;

    @Column(nullable = false)
    private String status;

    @Column(name = "joined_at", nullable = false)
    private Instant joinedAt;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Version
    @Column(nullable = false)
    private long version;

    protected TenantMembershipJpaEntity() {}

    public TenantMembershipJpaEntity(TenantMembership membership) {
        id = membership.id();
        tenantId = membership.tenantId();
        userId = membership.userId();
        role = membership.role();
        status = membership.status();
        joinedAt = membership.joinedAt();
        createdAt = membership.createdAt();
        version = membership.version();
    }

    public TenantMembership toDomain() {
        return new TenantMembership(
                id,
                tenantId,
                userId,
                role,
                status,
                joinedAt,
                createdAt,
                version);
    }
}
