package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.RoleRepository;
import com.agridrone.be1.identity.domain.SystemRole;
import com.agridrone.be1.identity.domain.UserStatus;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.RoleJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.RoleJpaRepository;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.UserJpaRepository;
import java.util.Optional;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Transactional;

@Repository
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class RolePersistenceAdapter implements RoleRepository {
    private final RoleJpaRepository roles;
    private final UserJpaRepository users;

    public RolePersistenceAdapter(RoleJpaRepository roles, UserJpaRepository users) {
        this.roles = roles;
        this.users = users;
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<SystemRole> findByCode(String code) {
        return roles.findByCode(code).map(RoleJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public boolean existsActiveUserWithRole(String code) {
        return users.existsByRoles_CodeAndStatusAndDeletedAtIsNull(code, UserStatus.ACTIVE);
    }
}
