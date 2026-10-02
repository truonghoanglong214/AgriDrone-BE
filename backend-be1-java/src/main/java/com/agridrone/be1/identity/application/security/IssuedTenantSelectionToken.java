package com.agridrone.be1.identity.application.security;

import java.time.Instant;

public record IssuedTenantSelectionToken(
        String value,
        Instant issuedAt,
        Instant expiresAt) {
}
