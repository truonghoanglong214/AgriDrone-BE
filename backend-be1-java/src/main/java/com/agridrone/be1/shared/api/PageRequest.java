package com.agridrone.be1.shared.api;

public record PageRequest(int pageNumber, int pageSize) {
    public static final int DEFAULT_PAGE_NUMBER = 1;
    public static final int DEFAULT_PAGE_SIZE = 20;
    public static final int MAXIMUM_SIZE = 100;

    public PageRequest {
        if (pageNumber < 1) {
            throw new IllegalArgumentException("pageNumber must be positive");
        }
        if (pageSize < 1 || pageSize > MAXIMUM_SIZE) {
            throw new IllegalArgumentException(
                    "pageSize must be between 1 and " + MAXIMUM_SIZE);
        }
    }

    public static PageRequest defaults() {
        return new PageRequest(DEFAULT_PAGE_NUMBER, DEFAULT_PAGE_SIZE);
    }

    public int zeroBasedPageIndex() {
        return pageNumber - 1;
    }
}
