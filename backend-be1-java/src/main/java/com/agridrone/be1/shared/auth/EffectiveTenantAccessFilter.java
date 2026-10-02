package com.agridrone.be1.shared.auth;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.UUID;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.web.filter.OncePerRequestFilter;

final class EffectiveTenantAccessFilter extends OncePerRequestFilter {
    private final EffectiveTenantAccessGuard guard;
    private final SecurityErrorWriter errors;

    EffectiveTenantAccessFilter(
            EffectiveTenantAccessGuard guard,
            SecurityErrorWriter errors) {
        this.guard = guard;
        this.errors = errors;
    }

    @Override
    protected void doFilterInternal(
            HttpServletRequest request,
            HttpServletResponse response,
            FilterChain chain) throws ServletException, IOException {
        Authentication authentication = SecurityContextHolder.getContext().getAuthentication();
        if (authentication == null
                || !authentication.isAuthenticated()
                || !(authentication.getPrincipal() instanceof Jwt jwt)
                || jwt.getClaimAsString("tenant_id") == null) {
            chain.doFilter(request, response);
            return;
        }

        UUID userId = uuid(jwt.getSubject());
        UUID tenantId = uuid(jwt.getClaimAsString("tenant_id"));
        UUID membershipId = uuid(jwt.getClaimAsString("tenant_membership_id"));
        if (userId == null || tenantId == null || membershipId == null) {
            errors.write(
                    request,
                    response,
                    HttpServletResponse.SC_FORBIDDEN,
                    "Tenant.AccessDenied",
                    "The tenant access context is invalid.");
            return;
        }

        EffectiveTenantAccessGuard.Decision decision =
                guard.evaluate(userId, tenantId, membershipId);
        if (!decision.granted()) {
            errors.write(
                    request,
                    response,
                    HttpServletResponse.SC_FORBIDDEN,
                    decision.errorCode(),
                    decision.message());
            return;
        }
        chain.doFilter(request, response);
    }

    private static UUID uuid(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        try {
            return UUID.fromString(value);
        } catch (IllegalArgumentException exception) {
            return null;
        }
    }
}
