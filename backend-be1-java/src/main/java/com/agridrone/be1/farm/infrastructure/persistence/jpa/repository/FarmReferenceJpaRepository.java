package com.agridrone.be1.farm.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.farm.infrastructure.persistence.jpa.entity.FarmReferenceJpaEntity;
import java.util.Collection;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface FarmReferenceJpaRepository
        extends JpaRepository<FarmReferenceJpaEntity, UUID> {
    Optional<FarmReferenceJpaEntity> findByIdAndStatusAndDeletedAtIsNull(
            UUID id, String status);

    List<FarmReferenceJpaEntity> findByIdInAndStatusAndDeletedAtIsNullOrderByCodeAscIdAsc(
            Collection<UUID> ids, String status);
}
