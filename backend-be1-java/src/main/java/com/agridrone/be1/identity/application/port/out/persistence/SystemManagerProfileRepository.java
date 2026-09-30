package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.SystemManagerProfile;
import java.util.Optional;
import java.util.UUID;

public interface SystemManagerProfileRepository {
    Optional<SystemManagerProfile> findById(UUID id);

    Optional<SystemManagerProfile> findByUserId(UUID userId);

    void save(SystemManagerProfile profile);
}
