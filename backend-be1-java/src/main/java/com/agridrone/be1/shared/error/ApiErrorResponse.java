package com.agridrone.be1.shared.error;

import java.time.Instant;
import java.util.List;

public record ApiErrorResponse(
        Instant timestamp,
        int status,
        String code,
        String message,
        String correlationId,
        String path,
        List<Violation> violations) {

    public ApiErrorResponse {
        violations = violations == null ? List.of() : List.copyOf(violations);
    }

    public record Violation(String field, String code, String message) {
    }
}
