package com.agridrone.be1.identity.infrastructure.persistence.jpa.repository;

import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.FarmManagerAssignmentJpaEntity;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;

public interface FarmManagerAssignmentJpaRepository
        extends JpaRepository<FarmManagerAssignmentJpaEntity, UUID> {

    Optional<FarmManagerAssignmentJpaEntity> findFirstByFarmIdAndEndedAtIsNull(UUID farmId);

    List<FarmManagerAssignmentJpaEntity> findByProfileIdAndEndedAtIsNull(UUID profileId);
}
