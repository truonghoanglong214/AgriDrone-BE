package com.agridrone.be1.shared.api;

public record PageRequest(int page, int size) {
    public static final int MAXIMUM_SIZE = 200;

    public PageRequest {
        if (page < 0) {
            throw new IllegalArgumentException("page must be non-negative");
        }
        if (size < 1 || size > MAXIMUM_SIZE) {
            throw new IllegalArgumentException("size must be between 1 and " + MAXIMUM_SIZE);
        }
    }
}
