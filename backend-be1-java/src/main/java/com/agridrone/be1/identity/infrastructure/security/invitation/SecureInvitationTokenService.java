package com.agridrone.be1.identity.infrastructure.security.invitation;

import com.agridrone.be1.identity.application.port.out.security.InvitationTokenService;
import com.agridrone.be1.identity.application.security.GeneratedInvitationToken;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;
import java.util.Base64;
import java.util.HexFormat;
import org.springframework.stereotype.Component;

@Component
public final class SecureInvitationTokenService implements InvitationTokenService {
    private static final int TOKEN_BYTES = 32;
    private static final SecureRandom RANDOM = new SecureRandom();
    private static final HexFormat HEX = HexFormat.of().withUpperCase();

    @Override
    public GeneratedInvitationToken generate() {
        byte[] bytes = new byte[TOKEN_BYTES];
        RANDOM.nextBytes(bytes);
        String plainText = Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);
        return new GeneratedInvitationToken(plainText, hash(plainText));
    }

    @Override
    public String hash(String plainTextToken) {
        if (plainTextToken == null || plainTextToken.isBlank()) {
            throw new IllegalArgumentException("Invitation token is required.");
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
