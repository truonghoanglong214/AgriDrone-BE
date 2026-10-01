package com.agridrone.be1.identity.application.service.resetpassword;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.agridrone.be1.identity.application.port.in.resetpassword.ResetPasswordCommand;
import com.agridrone.be1.identity.application.port.out.persistence.PasswordResetTokenRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.domain.PasswordResetToken;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.application.port.out.security.PasswordResetTokenGenerator;
import com.agridrone.be1.identity.infrastructure.security.passwordreset.SecurePasswordResetTokenGenerator;
import com.agridrone.be1.shared.error.StableApiException;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import org.junit.jupiter.api.Test;

class ResetPasswordServiceTest {
    private static final Instant NOW = Instant.parse("2026-10-01T00:00:00Z");

    @Test
    void consumesValidTokenAndChangesPassword() {
        PasswordResetTokenGenerator generator = new SecurePasswordResetTokenGenerator();
        String plaintext = "A".repeat(64);
        User user = User.create("admin@example.com", "old-hash", "Admin", null, NOW);
        PasswordResetToken token = PasswordResetToken.create(
                user.id(), generator.hash(plaintext), NOW.plusSeconds(1800), NOW.minusSeconds(1));
        PasswordResetTokenRepository tokens = org.mockito.Mockito.mock(PasswordResetTokenRepository.class);
        UserRepository users = org.mockito.Mockito.mock(UserRepository.class);
        PasswordHasher hasher = org.mockito.Mockito.mock(PasswordHasher.class);
        when(tokens.findByTokenHashForUpdate(token.tokenHash())).thenReturn(Optional.of(token));
        when(tokens.markUsed(token.id(), NOW)).thenReturn(true);
        when(users.findById(user.id())).thenReturn(Optional.of(user));
        when(hasher.hash("NewPassword123!")).thenReturn("new-hash");
        ResetPasswordService service = new ResetPasswordService(
                tokens, users, hasher, generator, Clock.fixed(NOW, ZoneOffset.UTC));

        service.reset(new ResetPasswordCommand(plaintext, "NewPassword123!", "NewPassword123!"));

        assertThat(user.passwordHash()).isEqualTo("new-hash");
        verify(users).save(user);
    }

    @Test
    void rejectsExpiredOrUnknownToken() {
        PasswordResetTokenRepository tokens = org.mockito.Mockito.mock(PasswordResetTokenRepository.class);
        when(tokens.findByTokenHashForUpdate(org.mockito.ArgumentMatchers.anyString()))
                .thenReturn(Optional.empty());
        ResetPasswordService service = new ResetPasswordService(
                tokens,
                org.mockito.Mockito.mock(UserRepository.class),
                org.mockito.Mockito.mock(PasswordHasher.class),
                new SecurePasswordResetTokenGenerator(),
                Clock.fixed(NOW, ZoneOffset.UTC));

        assertThatThrownBy(() -> service.reset(
                new ResetPasswordCommand("missing", "NewPassword123!", "NewPassword123!")))
                .isInstanceOf(StableApiException.class)
                .hasMessageContaining("invalid, expired");
    }
}
