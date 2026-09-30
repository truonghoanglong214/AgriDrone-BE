package com.agridrone.be1.shared.idempotency;

import com.fasterxml.jackson.databind.JsonNode;

public record IdempotencyResult(State state, Integer responseStatus, JsonNode responseBody) {
    public enum State { ACQUIRED, IN_PROGRESS, REPLAY }

    public static IdempotencyResult acquired() { return new IdempotencyResult(State.ACQUIRED, null, null); }
    public static IdempotencyResult inProgress() { return new IdempotencyResult(State.IN_PROGRESS, null, null); }
    public static IdempotencyResult replay(int status, JsonNode body) { return new IdempotencyResult(State.REPLAY, status, body); }
}
