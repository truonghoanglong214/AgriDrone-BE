package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.RoleJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.UserJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.RoleJpaRepository;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.UserJpaRepository;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import java.util.stream.Collectors;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Transactional;

@Repository
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class UserPersistenceAdapter implements UserRepository {
    private final UserJpaRepository users;
    private final RoleJpaRepository roles;

    public UserPersistenceAdapter(
            UserJpaRepository users,
            RoleJpaRepository roles) {
        this.users = users;
        this.roles = roles;
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<User> findById(UUID id) {
        return users.findByIdAndDeletedAtIsNull(id).map(UserJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<User> findByEmail(String email) {
        return users.findByEmailIgnoreCaseAndDeletedAtIsNull(email)
                .map(UserJpaEntity::toDomain);
    }

    @Override
    public Optional<User> findByEmailIncludingDeleted(String email) {
        return users.findByEmailIgnoreCase(email)
                .map(UserJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Set<String> findSystemRoleCodes(UUID userId) {
        return users.findById(userId)
                .map(UserJpaEntity::roles)
                .orElseGet(Set::of)
                .stream()
                .map(RoleJpaEntity::code)
                .collect(Collectors.toUnmodifiableSet());
    }

    @Override
    @Transactional
    public void save(User user) {
        UserJpaEntity entity = users.findById(user.id())
                .orElseGet(() -> new UserJpaEntity(user));
        entity.updateFrom(user);
        users.saveAndFlush(entity);
    }

    @Override
    @Transactional
    public void assignSystemRole(UUID userId, UUID roleId) {
        UserJpaEntity user = users.findById(userId)
                .orElseThrow(() -> new IllegalArgumentException("User does not exist"));
        RoleJpaEntity role = roles.findById(roleId)
                .orElseThrow(() -> new IllegalArgumentException("Role does not exist"));

        user.roles().add(role);
        users.saveAndFlush(user);
    }
}
