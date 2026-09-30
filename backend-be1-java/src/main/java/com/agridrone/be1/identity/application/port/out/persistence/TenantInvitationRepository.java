package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.TenantInvitation;
import java.time.Instant;
import java.util.Optional;
import java.util.UUID;

public interface TenantInvitationRepository {
    Optional<TenantInvitation> findByTokenHashForUpdate(String tokenHash);

    void add(TenantInvitation invitation);

    boolean markAccepted(UUID invitationId, UUID userId, Instant acceptedAt);
}
