package com.agridrone.be1.identity.application.service.resetpassword;

import com.agridrone.be1.identity.application.error.PasswordResetErrorCodes;
import com.agridrone.be1.identity.application.port.in.resetpassword.ResetPasswordCommand;
import com.agridrone.be1.identity.application.port.in.resetpassword.ResetPasswordResult;
import com.agridrone.be1.identity.application.port.in.resetpassword.ResetPasswordUseCase;
import com.agridrone.be1.identity.application.port.out.persistence.PasswordResetTokenRepository;
import com.agridrone.be1.identity.application.port.out.persistence.UserRepository;
import com.agridrone.be1.identity.application.port.out.security.PasswordHasher;
import com.agridrone.be1.identity.application.port.out.security.PasswordResetTokenGenerator;
import com.agridrone.be1.identity.domain.PasswordResetToken;
import com.agridrone.be1.identity.domain.User;
import java.nio.charset.StandardCharsets;
import java.time.Clock;
import java.time.Instant;
import org.springframework.http.HttpStatus;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import com.agridrone.be1.shared.error.StableApiException;

@Service
@ConditionalOnProperty(
        name = "agridrone.runtime.enabled",
        havingValue = "true",
        matchIfMissing = true)
public class ResetPasswordService implements ResetPasswordUseCase {
    private final PasswordResetTokenRepository tokens;
    private final UserRepository users;
    private final PasswordHasher passwordHasher;
    private final PasswordResetTokenGenerator tokenGenerator;
    private final Clock clock;

    public ResetPasswordService(
            PasswordResetTokenRepository tokens,
            UserRepository users,
            PasswordHasher passwordHasher,
            PasswordResetTokenGenerator tokenGenerator,
            Clock clock) {
        this.tokens = tokens;
        this.users = users;
        this.passwordHasher = passwordHasher;
        this.tokenGenerator = tokenGenerator;
        this.clock = clock;
    }

    @Override
    @Transactional
    public ResetPasswordResult reset(ResetPasswordCommand command) {
        validatePassword(command.newPassword(), command.confirmPassword());
        String tokenHash = tokenGenerator.hash(command.token());
        PasswordResetToken token = tokens.findByTokenHashForUpdate(tokenHash).orElseThrow(
                this::invalidToken);
        Instant now = clock.instant();
        if (!token.canUse(now)) {
            throw invalidToken();
        }

        User user = users.findById(token.userId()).orElseThrow(this::invalidToken);
        if (!user.isActive() || !tokens.markUsed(token.id(), now)) {
            throw invalidToken();
        }

        user.changePassword(passwordHasher.hash(command.newPassword()), now);
        users.save(user);
        return new ResetPasswordResult("Password has been reset successfully.");
    }

    private static void validatePassword(String password, String confirmation) {
        if (password == null || password.isBlank()
                || password.getBytes(StandardCharsets.UTF_8).length < 8
                || password.getBytes(StandardCharsets.UTF_8).length > 72
                || !password.equals(confirmation)) {
            throw new StableApiException(
                    HttpStatus.UNPROCESSABLE_ENTITY,
                    "Validation.Failed",
                    "Password does not satisfy the password policy.");
        }
    }

    private StableApiException invalidToken() {
        return new StableApiException(
                HttpStatus.UNPROCESSABLE_ENTITY,
                PasswordResetErrorCodes.INVALID_OR_EXPIRED_TOKEN,
                "The password reset token is invalid, expired, or has already been used.");
    }
}
