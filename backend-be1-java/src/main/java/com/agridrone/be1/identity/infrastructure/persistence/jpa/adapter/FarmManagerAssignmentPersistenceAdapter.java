package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.FarmManagerAssignmentRepository;
import com.agridrone.be1.identity.domain.FarmManagerAssignment;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.FarmManagerAssignmentJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.FarmManagerAssignmentJpaRepository;
import java.util.List;
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
public class FarmManagerAssignmentPersistenceAdapter
        implements FarmManagerAssignmentRepository {

    private final FarmManagerAssignmentJpaRepository assignments;

    public FarmManagerAssignmentPersistenceAdapter(
            FarmManagerAssignmentJpaRepository assignments) {
        this.assignments = assignments;
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<FarmManagerAssignment> findActiveByFarmId(UUID farmId) {
        return assignments.findFirstByFarmIdAndEndedAtIsNull(farmId)
                .map(FarmManagerAssignmentJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public List<FarmManagerAssignment> findActiveByProfileId(UUID profileId) {
        return assignments.findByProfileIdAndEndedAtIsNull(profileId).stream()
                .map(FarmManagerAssignmentJpaEntity::toDomain)
                .toList();
    }

    @Override
    @Transactional
    public void add(FarmManagerAssignment assignment) {
        assignments.saveAndFlush(new FarmManagerAssignmentJpaEntity(assignment));
    }

    @Override
    @Transactional
    public boolean end(FarmManagerAssignment assignment, long expectedVersion) {
        Optional<FarmManagerAssignmentJpaEntity> existing = assignments.findById(assignment.id());
        if (existing.isEmpty()) {
            return false;
        }

        FarmManagerAssignmentJpaEntity entity = existing.get();
        if (!entity.applyEnd(assignment, expectedVersion)) {
            return false;
        }

        assignments.saveAndFlush(entity);
        return true;
    }
}
