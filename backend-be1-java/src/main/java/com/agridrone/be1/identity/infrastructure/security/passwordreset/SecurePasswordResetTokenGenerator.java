package com.agridrone.be1.identity.infrastructure.security.passwordreset;

import com.agridrone.be1.identity.application.port.out.security.PasswordResetTokenGenerator;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;
import java.util.HexFormat;
import org.springframework.stereotype.Component;

@Component
public class SecurePasswordResetTokenGenerator implements PasswordResetTokenGenerator {
    private static final int TOKEN_BYTES = 32;
    private static final SecureRandom RANDOM = new SecureRandom();
    private static final HexFormat HEX = HexFormat.of().withUpperCase();

    @Override
    public String generatePlainTextToken() {
        byte[] bytes = new byte[TOKEN_BYTES];
        RANDOM.nextBytes(bytes);
        return HEX.formatHex(bytes);
    }

    @Override
    public String hash(String plainTextToken) {
        if (plainTextToken == null || plainTextToken.isBlank()) {
            throw new IllegalArgumentException("Password reset token is required.");
        }
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                    .digest(plainTextToken.trim().getBytes(StandardCharsets.UTF_8));
            return HEX.formatHex(digest);
        } catch (NoSuchAlgorithmException exception) {
            throw new IllegalStateException("SHA-256 is unavailable.", exception);
        }
    }
}
