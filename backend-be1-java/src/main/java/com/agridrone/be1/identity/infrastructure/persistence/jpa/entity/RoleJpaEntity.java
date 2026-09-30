package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import com.agridrone.be1.identity.domain.SystemRole;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.util.UUID;

@Entity
@Table(name = "roles", schema = "identity")
public class RoleJpaEntity {
    @Id
    private UUID id;

    @Column(nullable = false, unique = true)
    private String code;

    protected RoleJpaEntity() {}

    public UUID id() {
        return id;
    }

    public String code() {
        return code;
    }

    public SystemRole toDomain() {
        return new SystemRole(id, code);
    }
}
