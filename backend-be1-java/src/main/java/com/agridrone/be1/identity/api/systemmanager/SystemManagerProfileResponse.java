package com.agridrone.be1.identity.api.systemmanager;

import java.time.Instant;
import java.util.UUID;

public record SystemManagerProfileResponse(
        UUID id,
        UUID userId,
        String email,
        String fullName,
        int status,
        int availability,
        int qualificationStatus,
        Instant qualificationExpiresAt,
        long version) {}
