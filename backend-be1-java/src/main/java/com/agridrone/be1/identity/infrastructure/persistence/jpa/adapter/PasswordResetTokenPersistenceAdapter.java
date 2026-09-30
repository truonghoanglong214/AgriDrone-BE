package com.agridrone.be1.identity.infrastructure.persistence.jpa.adapter;

import com.agridrone.be1.identity.application.port.out.persistence.PasswordResetTokenRepository;
import com.agridrone.be1.identity.domain.PasswordResetToken;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.entity.PasswordResetTokenJpaEntity;
import com.agridrone.be1.identity.infrastructure.persistence.jpa.repository.PasswordResetTokenJpaRepository;
import java.time.Instant;
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
public class PasswordResetTokenPersistenceAdapter implements PasswordResetTokenRepository {
    private final PasswordResetTokenJpaRepository tokens;

    public PasswordResetTokenPersistenceAdapter(PasswordResetTokenJpaRepository tokens) {
        this.tokens = tokens;
    }

    @Override
    @Transactional
    public Optional<PasswordResetToken> findByTokenHashForUpdate(String tokenHash) {
        return tokens.findByTokenHash(tokenHash).map(PasswordResetTokenJpaEntity::toDomain);
    }

    @Override
    @Transactional
    public void add(PasswordResetToken token) {
        tokens.saveAndFlush(new PasswordResetTokenJpaEntity(token));
    }

    @Override
    @Transactional
    public void revokeActiveForUser(UUID userId, Instant revokedAt) {
        List<PasswordResetTokenJpaEntity> activeTokens =
                tokens.findByUserIdAndUsedAtIsNullAndRevokedAtIsNull(userId);

        activeTokens.forEach(token -> token.revoke(revokedAt));
        tokens.saveAllAndFlush(activeTokens);
    }

    @Override
    @Transactional
    public boolean markUsed(UUID tokenId, Instant usedAt) {
        Optional<PasswordResetTokenJpaEntity> existing = tokens.findById(tokenId);
        if (existing.isEmpty() || !existing.get().markUsed(usedAt)) {
            return false;
        }

        tokens.saveAndFlush(existing.get());
        return true;
    }
}
