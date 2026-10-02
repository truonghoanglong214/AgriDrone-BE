package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.TenantMembershipRepository;
import com.agridrone.be1.identity.application.readmodel.UserTenantListItem;
import com.agridrone.be1.identity.domain.TenantMembership;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.TenantMembershipJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.TenantMembershipJpaRepository;
import com.agridrone.be1.shared.api.PageRequest;
import com.agridrone.be1.shared.api.PageResponse;
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
public class TenantMembershipPersistenceAdapter implements TenantMembershipRepository {
    private static final String OWNER = "OWNER";
    private static final String ACTIVE = "ACTIVE";

    private final TenantMembershipJpaRepository memberships;

    public TenantMembershipPersistenceAdapter(TenantMembershipJpaRepository memberships) {
        this.memberships = memberships;
    }

    @Override
    @Transactional(readOnly = true)
    public List<TenantMembership> findActiveByUserId(UUID userId) {
        return memberships.findByUserIdAndStatus(userId, ACTIVE).stream()
                .map(TenantMembershipJpaEntity::toDomain)
                .toList();
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<TenantMembership> findActive(UUID userId, UUID tenantId) {
        return memberships.findByUserIdAndTenantIdAndStatus(userId, tenantId, ACTIVE)
                .map(TenantMembershipJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public Optional<TenantMembership> find(UUID userId, UUID tenantId) {
        return memberships.findByUserIdAndTenantId(userId, tenantId)
                .map(TenantMembershipJpaEntity::toDomain);
    }

    @Override
    @Transactional(readOnly = true)
    public boolean hasActiveOwner(UUID tenantId) {
        return memberships.existsByTenantIdAndRoleAndStatus(tenantId, OWNER, ACTIVE);
    }

    @Override
    @Transactional(readOnly = true)
    public PageResponse<UserTenantListItem> findPageByUserId(
            UUID userId,
            PageRequest pageRequest) {
        var page = memberships.findActivePageByUserId(
                userId,
                org.springframework.data.domain.PageRequest.of(
                        pageRequest.zeroBasedPageIndex(),
                        pageRequest.pageSize()));
        return PageResponse.of(page.getContent(), pageRequest, page.getTotalElements());
    }

    @Override
    @Transactional
    public void add(TenantMembership membership) {
        memberships.saveAndFlush(new TenantMembershipJpaEntity(membership));
    }
}
