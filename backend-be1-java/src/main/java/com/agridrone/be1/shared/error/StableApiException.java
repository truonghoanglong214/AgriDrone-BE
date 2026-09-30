package com.agridrone.be1.shared.error;

import org.springframework.http.HttpStatus;

public final class StableApiException extends RuntimeException {
    private final HttpStatus status;
    private final String code;

    public StableApiException(HttpStatus status, String code, String message) {
        super(message);
        this.status = status;
        this.code = code;
    }

    public HttpStatus status() {
        return status;
    }

    public String code() {
        return code;
    }
}
