package com.agridrone.be1.shared.messaging;

public class PermanentMessageException extends RuntimeException {
    private final String code;

    public PermanentMessageException(String code, String message) {
        super(message);
        this.code = code;
    }

    public String code() {
        return code;
    }
}
