package com.agridrone.be1.identity.domain;

import java.util.Objects;
import java.util.UUID;

public record SystemRole(
        UUID id,
        String code) {
    public SystemRole {
        Objects.requireNonNull(id, "id");

        if (!SystemRoleCodes.contains(code)) {
            throw new IllegalArgumentException("Unsupported system role code: " + code);
        }
    }
}
