package com.agridrone.be1.shared.execution;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.Set;
import java.util.UUID;
import java.util.stream.Collectors;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

@Component
@Order(Ordered.HIGHEST_PRECEDENCE + 1)
public final class ExecutionContextFilter extends OncePerRequestFilter {
    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain chain)
            throws ServletException, IOException {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        UUID actorId = null;
        UUID tenantId = null;
        ActorType actorType = ActorType.SYSTEM;
        Set<String> roles = Set.of();
        if (authentication != null && authentication.isAuthenticated() && authentication.getPrincipal() instanceof Jwt jwt) {
            actorId = uuid(jwt.getSubject());
            tenantId = uuid(jwt.getClaimAsString("tenant_id"));
            actorType = actorId == null ? ActorType.SYSTEM : ActorType.USER;
            roles = authentication.getAuthorities().stream().map(Object::toString).collect(Collectors.toUnmodifiableSet());
        }
        UUID correlationId = uuid(CorrelationIds.currentOrCreate());
        try (ExecutionContext.Scope ignored = ExecutionContext.begin(new ExecutionContextSnapshot(
                tenantId, actorId, actorType, correlationId, null, ExecutionSource.HTTP, roles))) {
            chain.doFilter(request, response);
        }
    }

    private static UUID uuid(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        try {
            return UUID.fromString(value);
        } catch (IllegalArgumentException ignored) {
            return null;
        }
    }
}
