package com.agridrone.be1.shared.idempotency;

public record IdempotencyRecord(
        String requestHash,
        String status,
        Integer responseStatus,
        String responseBody) {
}
