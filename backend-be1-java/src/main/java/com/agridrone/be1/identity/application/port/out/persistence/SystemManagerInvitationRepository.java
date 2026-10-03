package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.SystemManagerInvitation;
import java.time.Instant;
import java.util.Optional;
import java.util.UUID;

public interface SystemManagerInvitationRepository {
    Optional<SystemManagerInvitation> findByTokenHash(String tokenHash);

    Optional<SystemManagerInvitation> findByTokenHashForUpdate(String tokenHash);

    Optional<SystemManagerInvitation> findPendingByEmail(String email);

    void add(SystemManagerInvitation invitation);

    void save(SystemManagerInvitation invitation);

    boolean markAccepted(UUID invitationId, UUID userId, Instant acceptedAt);
}
