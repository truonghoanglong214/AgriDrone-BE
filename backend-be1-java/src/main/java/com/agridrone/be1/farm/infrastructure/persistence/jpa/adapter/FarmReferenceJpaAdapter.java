package com.agridrone.be1.farm.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.farm.infrastructure.persistence.jpa.entity.FarmReferenceJpaEntity;
import com.agridrone.be1.farm.infrastructure.persistence.jpa.repository.FarmReferenceJpaRepository;
import com.agridrone.be1.identity.application.model.FarmReference;
import com.agridrone.be1.identity.application.port.out.reference.FarmReferenceQuery;
import java.util.Collection;
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
public class FarmReferenceJpaAdapter implements FarmReferenceQuery {
    private static final String ACTIVE = "ACTIVE";
    private final FarmReferenceJpaRepository farms;

    public FarmReferenceJpaAdapter(FarmReferenceJpaRepository farms) {
        this.farms = farms;
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<FarmReference> findActiveById(UUID farmId) {
        return farms.findByIdAndStatusAndDeletedAtIsNull(farmId, ACTIVE)
                .map(FarmReferenceJpaAdapter::map);
    }

    @Override
    @Transactional(readOnly = true)
    public List<FarmReference> findActiveByIds(Collection<UUID> farmIds) {
        if (farmIds.isEmpty()) {
            return List.of();
        }
        return farms.findByIdInAndStatusAndDeletedAtIsNullOrderByCodeAscIdAsc(
                        farmIds, ACTIVE)
                .stream()
                .map(FarmReferenceJpaAdapter::map)
                .toList();
    }

    private static FarmReference map(FarmReferenceJpaEntity farm) {
        return new FarmReference(
                farm.tenantId(), farm.id(), farm.code(), farm.name(),
                farm.address(), farm.areaHectares());
    }
}
