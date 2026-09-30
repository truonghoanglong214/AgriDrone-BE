package com.agridrone.be1.identity.application.port.out.security;

public interface PasswordHasher {
    String hash(String plainTextPassword);

    boolean matches(String plainTextPassword, String passwordHash);
}
