package com.agridrone.be1.identity.application.security;

import java.time.Instant;

public record IssuedAccessToken(String value, Instant issuedAt, Instant expiresAt) {
}
