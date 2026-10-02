package com.agridrone.be1.shared.api;

import java.util.List;
import java.util.function.Function;

public record PageResponse<T>(
        List<T> items,
        int pageNumber,
        int pageSize,
        long totalCount,
        long totalPages,
        boolean hasPreviousPage,
        boolean hasNextPage) {
    public PageResponse {
        items = List.copyOf(items);
        if (pageNumber < 1 || pageSize < 1 || totalCount < 0 || totalPages < 0) {
            throw new IllegalArgumentException("pagination metadata is invalid");
        }
    }

    public static <T> PageResponse<T> of(
            List<T> items,
            PageRequest request,
            long totalCount) {
        long pages = totalCount == 0
                ? 0
                : (totalCount + request.pageSize() - 1) / request.pageSize();
        return new PageResponse<>(
                items,
                request.pageNumber(),
                request.pageSize(),
                totalCount,
                pages,
                request.pageNumber() > 1,
                request.pageNumber() < pages);
    }

    public <R> PageResponse<R> map(Function<T, R> mapper) {
        return new PageResponse<>(
                items.stream().map(mapper).toList(),
                pageNumber,
                pageSize,
                totalCount,
                totalPages,
                hasPreviousPage,
                hasNextPage);
    }
}
