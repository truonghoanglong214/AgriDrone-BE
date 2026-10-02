package com.agridrone.be1.identity.application.port.in.loginuser;

import java.time.Instant;
import java.util.List;
import java.util.UUID;

public record LoginUserResult(
        String email,
        String fullName,
        String phone,
        Session session,
        TenantSelection tenantSelection) {

    public record Session(
            String accessToken,
            Instant expiresAt,
            Tenant tenant) {
    }

    public record Tenant(
            UUID id,
            String code,
            String name,
            String role) {
    }

    public record TenantSelection(
            String selectionToken,
            Instant expiresAt,
            List<Tenant> tenants) {
        public TenantSelection {
            tenants = List.copyOf(tenants);
        }
    }
}
