package com.agridrone.be1.identity.infrastructure.persistence.jpa.entity;

import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.domain.UserStatus;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.JoinTable;
import jakarta.persistence.ManyToMany;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.HashSet;
import java.util.Set;
import java.util.UUID;

@Entity
@Table(name = "users", schema = "identity")
public class UserJpaEntity {
    @Id
    private UUID id;

    @Column(nullable = false)
    private String email;

    @Column(name = "password_hash", nullable = false)
    private String passwordHash;

    @Column(name = "full_name", nullable = false)
    private String fullName;

    private String phone;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private UserStatus status;

    @Column(name = "last_login_at")
    private Instant lastLoginAt;

    @Column(name = "created_at", nullable = false, updatable = false)
    private Instant createdAt;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @Column(name = "deleted_at")
    private Instant deletedAt;

    @ManyToMany(fetch = FetchType.LAZY)
    @JoinTable(
            name = "user_roles",
            schema = "identity",
            joinColumns = @JoinColumn(name = "user_id"),
            inverseJoinColumns = @JoinColumn(name = "role_id"))
    private Set<RoleJpaEntity> roles = new HashSet<>();

    protected UserJpaEntity() {}

    public UserJpaEntity(User user) {
        id = user.id();
        createdAt = user.createdAt();
        updateFrom(user);
    }

    public void updateFrom(User user) {
        email = user.email();
        passwordHash = user.passwordHash();
        fullName = user.fullName();
        phone = user.phone();
        status = user.status();
        lastLoginAt = user.lastLoginAt();
        updatedAt = user.updatedAt();
        deletedAt = user.deletedAt();
    }

    public User toDomain() {
        return new User(
                id,
                email,
                passwordHash,
                fullName,
                phone,
                status,
                lastLoginAt,
                createdAt,
                updatedAt,
                deletedAt);
    }

    public Set<RoleJpaEntity> roles() {
        return roles;
    }
}
