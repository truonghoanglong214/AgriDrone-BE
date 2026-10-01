package com.agridrone.be1.identity.application.port.in.loginuser;

import java.time.Instant;
import java.util.UUID;

public record LoginUserResult(
        String email,
        String fullName,
        String phone,
        Session session) {

    public record Session(
            String accessToken,
            Instant expiresAt,
            Tenant tenant) {
    }

    public record Tenant(
            UUID id,
            String code,
            String name,
            String role) {
    }
}
