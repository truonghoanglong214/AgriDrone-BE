package com.agridrone.be1.identity.application.exception;


public class BootstrapException extends RuntimeException{
    private final String code;

    public BootstrapException(String code)
    {
        super(code);
        this.code = code;
    }

    public BootstrapException(String code, String message) {
        super(message);
        this.code = code;
    }

    public String getCode() {
        return code;
    }
}
