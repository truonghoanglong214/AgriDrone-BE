package com.agridrone.be1.shared.execution;

import java.util.UUID;
import org.slf4j.MDC;

public final class CorrelationIds {

    public static final String MDC_KEY = "correlationId";

    private CorrelationIds() {
    }

    public static String normalizeOrCreate(String candidate) {
        if (candidate != null) {
            try {
                return UUID.fromString(candidate.strip()).toString();
            } catch (IllegalArgumentException ignored) {
                // Untrusted input is replaced with a server-generated identifier.
            }
        }

        return UUID.randomUUID().toString();
    }

    public static String currentOrCreate() {
        String current = MDC.get(MDC_KEY);
        return current == null ? UUID.randomUUID().toString() : current;
    }
}
