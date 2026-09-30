package com.agridrone.be1.shared.execution;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import org.slf4j.MDC;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

@Component
@Order(Ordered.HIGHEST_PRECEDENCE)
public final class CorrelationIdFilter extends OncePerRequestFilter {

    private final String headerName;

    public CorrelationIdFilter(WebPlatformProperties properties) {
        this.headerName = properties.correlationHeader();
    }

    @Override
    protected void doFilterInternal(
            HttpServletRequest request,
            HttpServletResponse response,
            FilterChain filterChain) throws ServletException, IOException {
        String correlationId = CorrelationIds.normalizeOrCreate(request.getHeader(headerName));

        try (MDC.MDCCloseable ignored = MDC.putCloseable(CorrelationIds.MDC_KEY, correlationId)) {
            response.setHeader(headerName, correlationId);
            filterChain.doFilter(request, response);
        }
    }
}
