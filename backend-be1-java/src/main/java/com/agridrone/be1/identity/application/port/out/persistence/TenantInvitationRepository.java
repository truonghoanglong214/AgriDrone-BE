package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.TenantInvitation;
import java.time.Instant;
import java.util.Optional;
import java.util.UUID;

public interface TenantInvitationRepository {
    Optional<TenantInvitation> findByTokenHash(String tokenHash);

    Optional<TenantInvitation> findByTokenHashForUpdate(String tokenHash);

    Optional<TenantInvitation> findPendingOwnerProvisioning(UUID tenantId);

    void add(TenantInvitation invitation);

    void save(TenantInvitation invitation);

    boolean markAccepted(UUID invitationId, UUID userId, Instant acceptedAt);
}
