package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.FarmManagerAssignment;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface FarmManagerAssignmentRepository {
    Optional<FarmManagerAssignment> findActiveByFarmId(UUID farmId);

    List<FarmManagerAssignment> findActiveByProfileId(UUID profileId);

    void add(FarmManagerAssignment assignment);

    boolean end(FarmManagerAssignment assignment, long expectedVersion);
}
