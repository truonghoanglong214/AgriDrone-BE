package com.agridrone.be1.identity.application.port.out.persistence;

import com.agridrone.be1.identity.domain.PasswordResetToken;
import java.time.Instant;
import java.util.Optional;
import java.util.UUID;

public interface PasswordResetTokenRepository {
    Optional<PasswordResetToken> findByTokenHashForUpdate(String tokenHash);

    void add(PasswordResetToken token);

    void revokeActiveForUser(UUID userId, Instant revokedAt);

    boolean markUsed(UUID tokenId, Instant usedAt);
}
