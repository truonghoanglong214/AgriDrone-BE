package com.agridrone.be1.identity.application.service.forgotpassword;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

import com.agridrone.be1.identity.application.port.out.notification.PasswordResetDelivery;
import com.agridrone.be1.identity.application.port.out.persistence.PasswordResetTokenRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.domain.User;
import com.agridrone.be1.identity.infrastructure.config.PasswordResetProperties;
import com.agridrone.be1.identity.application.port.out.security.PasswordResetTokenGenerator;
import com.agridrone.be1.identity.infrastructure.security.passwordreset.SecurePasswordResetTokenGenerator;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import org.junit.jupiter.api.Test;

class ForgotPasswordServiceTest {
    private static final Instant NOW = Instant.parse("2026-10-01T00:00:00Z");

    @Test
    void createsOneHashedTokenAndDeliversPlaintextTokenForActiveUser() {
        UserRepository users = org.mockito.Mockito.mock(UserRepository.class);
        PasswordResetTokenRepository tokens = org.mockito.Mockito.mock(PasswordResetTokenRepository.class);
        PasswordResetDelivery delivery = org.mockito.Mockito.mock(PasswordResetDelivery.class);
        PasswordResetTokenGenerator generator = new SecurePasswordResetTokenGenerator();
        User user = User.create("admin@example.com", "hash", "Admin", null, NOW);
        when(users.findByEmail("admin@example.com")).thenReturn(Optional.of(user));
        ForgotPasswordService service = new ForgotPasswordService(
                users, tokens, generator, delivery,
                new PasswordResetProperties("http://localhost/reset", Duration.ofMinutes(30)),
                Clock.fixed(NOW, ZoneOffset.UTC));

        service.requestReset(new com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordCommand(
                " Admin@Example.com "));

        verify(tokens).revokeActiveForUser(user.id(), NOW);
        verify(tokens).add(any());
        verify(delivery).send(eq(user.email()), eq(user.fullName()), any(String.class), any(Instant.class));
    }

    @Test
    void doesNotRevealWhetherUnknownEmailExists() {
        UserRepository users = org.mockito.Mockito.mock(UserRepository.class);
        PasswordResetTokenRepository tokens = org.mockito.Mockito.mock(PasswordResetTokenRepository.class);
        PasswordResetDelivery delivery = org.mockito.Mockito.mock(PasswordResetDelivery.class);
        when(users.findByEmail("missing@example.com")).thenReturn(Optional.empty());
        ForgotPasswordService service = new ForgotPasswordService(
                users, tokens, new SecurePasswordResetTokenGenerator(), delivery,
                new PasswordResetProperties("http://localhost/reset", Duration.ofMinutes(30)),
                Clock.fixed(NOW, ZoneOffset.UTC));

        service.requestReset(new com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordCommand(
                "missing@example.com"));

        verifyNoInteractions(tokens, delivery);
    }
}
