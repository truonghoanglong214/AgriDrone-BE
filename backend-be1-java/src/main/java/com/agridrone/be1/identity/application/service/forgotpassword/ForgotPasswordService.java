package com.agridrone.be1.identity.application.service.forgotpassword;

import com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordCommand;
import com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordResult;
import com.agridrone.be1.identity.application.port.in.forgotpassword.ForgotPasswordUseCase;
import com.agridrone.be1.identity.application.port.out.notification.PasswordResetDelivery;
import com.agridrone.be1.identity.application.port.out.notification.PasswordResetPolicy;
import com.agridrone.be1.identity.application.port.out.persistence.PasswordResetTokenRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.PasswordResetTokenGenerator;
import com.agridrone.be1.identity.domain.PasswordResetToken;
import com.agridrone.be1.identity.domain.User;
import java.time.Clock;
import java.time.Instant;
import java.util.Locale;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
@ConditionalOnProperty(
        prefix = "agridrone.notification.smtp",
        name = "enabled",
        havingValue = "true")
public class ForgotPasswordService implements ForgotPasswordUseCase {
    private static final String GENERIC_MESSAGE =
            "If an account exists for this email, a password reset link has been sent.";
    private static final Logger LOGGER = LoggerFactory.getLogger(ForgotPasswordService.class);

    private final UserRepository users;
    private final PasswordResetTokenRepository tokens;
    private final PasswordResetTokenGenerator tokenGenerator;
    private final PasswordResetDelivery delivery;
    private final PasswordResetPolicy policy;
    private final Clock clock;

    public ForgotPasswordService(
            UserRepository users,
            PasswordResetTokenRepository tokens,
            PasswordResetTokenGenerator tokenGenerator,
            PasswordResetDelivery delivery,
            PasswordResetPolicy policy,
            Clock clock) {
        this.users = users;
        this.tokens = tokens;
        this.tokenGenerator = tokenGenerator;
        this.delivery = delivery;
        this.policy = policy;
        this.clock = clock;
    }

    @Override
    @Transactional
    public ForgotPasswordResult requestReset(ForgotPasswordCommand command) {
        String email = command.email().trim().toLowerCase(Locale.ROOT);
        User user = users.findByEmail(email).orElse(null);
        if (user == null || !user.isActive()) {
            return new ForgotPasswordResult(GENERIC_MESSAGE);
        }

        Instant now = clock.instant();
        String plainTextToken = tokenGenerator.generatePlainTextToken();
        tokens.revokeActiveForUser(user.id(), now);
        PasswordResetToken token = PasswordResetToken.create(
                user.id(), tokenGenerator.hash(plainTextToken), now.plus(policy.expiration()), now);
        tokens.add(token);

        try {
            delivery.send(user.email(), user.fullName(), plainTextToken, token.expiresAt());
        } catch (RuntimeException exception) {
            LOGGER.error("Failed to send password reset email for userId={}", user.id(), exception);
        }
        return new ForgotPasswordResult(GENERIC_MESSAGE);
    }
}
