package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Version;

@Entity
@Table(name = "initialization_locks", schema = "identity")
public class InitializationLockJpaEntity {
    @Id
    private String name;

    @Version
    private long version;

    protected InitializationLockJpaEntity() {
    }
}
