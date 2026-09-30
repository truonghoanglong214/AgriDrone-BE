package com.agridrone.be1.shared.api;

import java.util.List;

public record PageResponse<T>(List<T> items, int page, int size, long totalItems, int totalPages) {
    public PageResponse {
        items = List.copyOf(items);
        if (page < 0 || size < 1 || totalItems < 0 || totalPages < 0) {
            throw new IllegalArgumentException("pagination metadata is invalid");
        }
    }

    public static <T> PageResponse<T> of(List<T> items, PageRequest request, long totalItems) {
        int pages = totalItems == 0 ? 0 : Math.toIntExact((totalItems + request.size() - 1) / request.size());
        return new PageResponse<>(items, request.page(), request.size(), totalItems, pages);
    }
}
