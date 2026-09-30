package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.SystemManagerProfileRepository;
import com.agridrone.be1.identity.domain.SystemManagerProfile;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.SystemManagerProfileJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.SystemManagerProfileJpaRepository;
import java.util.Optional;
import java.util.UUID;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Repository;
import org.springframework.transaction.annotation.Transactional;

@Repository
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class SystemManagerProfilePersistenceAdapter implements SystemManagerProfileRepository {
    private final SystemManagerProfileJpaRepository profiles;

    public SystemManagerProfilePersistenceAdapter(
            SystemManagerProfileJpaRepository profiles) {
        this.profiles = profiles;
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<SystemManagerProfile> findById(UUID id) {
        return profiles.findById(id).map(SystemManagerProfileJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<SystemManagerProfile> findByUserId(UUID userId) {
        return profiles.findByUserId(userId).map(SystemManagerProfileJpaEntity::toDomain);
    }

    @Override
    @Transactional
    public void save(SystemManagerProfile profile) {
        Optional<SystemManagerProfileJpaEntity> existing = profiles.findById(profile.id());

        if (existing.isEmpty()) {
            if (profile.version() != 1) {
                throw new IllegalStateException(
                        "A new system manager profile must start at version 1");
            }
            profiles.saveAndFlush(new SystemManagerProfileJpaEntity(profile));
            return;
        }

        SystemManagerProfileJpaEntity entity = existing.get();
        if (entity.toDomain().version() == profile.version()) {
            return;
        }

        entity.applyMutation(profile);
        profiles.saveAndFlush(entity);
    }
}
