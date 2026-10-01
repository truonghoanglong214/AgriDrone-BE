package com.agridrone.be1.identity.application.port.out.security;

public interface PasswordResetTokenGenerator {
    String generatePlainTextToken();

    String hash(String plainTextToken);
}
