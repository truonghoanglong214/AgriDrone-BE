package com.agridrone.be1.shared.api;

import com.agridrone.be1.shared.auth.SecurityErrorWriter;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.Set;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpMethod;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

/** Keeps intentionally retired mutation routes fail-closed during the Java cutover. */
@Component
@Order(Ordered.HIGHEST_PRECEDENCE + 1)
public final class LegacyEndpointFilter extends OncePerRequestFilter {
    private static final Set<String> EXACT_POST_PATHS = Set.of(
            "/api/auth/register",
            "/api/farms",
            "/api/tenants/current/transfer-ownership");

    private final SecurityErrorWriter errorWriter;

    public LegacyEndpointFilter(SecurityErrorWriter errorWriter) {
        this.errorWriter = errorWriter;
    }

    @Override
    protected void doFilterInternal(
            HttpServletRequest request,
            HttpServletResponse response,
            FilterChain filterChain) throws ServletException, IOException {
        if (isDisabledLegacyMutation(request)) {
            errorWriter.write(
                    request,
                    response,
                    HttpStatus.GONE.value(),
                    "LegacyFlow.Disabled",
                    "This legacy operation is disabled.");
            return;
        }
        filterChain.doFilter(request, response);
    }

    private static boolean isDisabledLegacyMutation(HttpServletRequest request) {
        String path = request.getRequestURI();
        if (HttpMethod.POST.matches(request.getMethod()) && EXACT_POST_PATHS.contains(path)) {
            return true;
        }
        return HttpMethod.PUT.matches(request.getMethod())
                && path.matches("/api/farms/[^/]+/restore");
    }
}
