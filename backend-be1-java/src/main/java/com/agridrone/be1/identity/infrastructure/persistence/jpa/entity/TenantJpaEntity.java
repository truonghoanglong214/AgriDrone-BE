package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import com.agridrone.be1.identity.domain.Tenant;
import com.agridrone.be1.identity.domain.TenantStatus;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.UUID;

@Entity
@Table(name = "tenants", schema = "identity")
public class TenantJpaEntity {
    @Id
    private UUID id;

    @Column(nullable = false)
    private String code;

    @Column(nullable = false)
    private String name;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private TenantStatus status;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @Column(name = "deleted_at")
    private Instant deletedAt;

    protected TenantJpaEntity() {}

    public TenantJpaEntity(Tenant tenant) {
        id = tenant.id();
        createdAt = tenant.createdAt();
        updateFrom(tenant);
    }

    public void updateFrom(Tenant tenant) {
        code = tenant.code();
        name = tenant.name();
        status = tenant.status();
        updatedAt = tenant.updatedAt();
        deletedAt = tenant.deletedAt();
    }

    public Tenant toDomain() {
        return new Tenant(id, code, name, status, createdAt, updatedAt, deletedAt);
    }
}
