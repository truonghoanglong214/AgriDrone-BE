package com.agridrone.be1.identity.application.port.out.reference;

import com.agridrone.be1.identity.application.model.FarmReference;
import java.util.Collection;
import java.util.List;
import java.util.Optional;
import java.util.UUID;

public interface FarmReferenceQuery {
    Optional<FarmReference> findActiveById(UUID farmId);

    List<FarmReference> findActiveByIds(Collection<UUID> farmIds);
}
