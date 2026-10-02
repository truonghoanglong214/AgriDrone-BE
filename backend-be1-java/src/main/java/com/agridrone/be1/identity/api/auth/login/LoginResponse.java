package com.agridrone.be1.identity.api.auth.login;

import com.agridrone.be1.identity.application.port.in.loginuser.LoginUserResult;
import java.time.Instant;
import java.util.List;
import java.util.UUID;

public record LoginResponse(
        String email,
        String fullName,
        String phone,
        Session session,
        TenantSelection tenantSelection) {

    public static LoginResponse from(LoginUserResult result) {
        LoginUserResult.Session source = result.session();
        Session session = source == null ? null : new Session(
                source.accessToken(), source.expiresAt(), toTenant(source.tenant()));
        LoginUserResult.TenantSelection sourceSelection = result.tenantSelection();
        TenantSelection tenantSelection = sourceSelection == null
                ? null
                : new TenantSelection(
                        sourceSelection.selectionToken(),
                        sourceSelection.expiresAt(),
                        sourceSelection.tenants().stream()
                                .map(LoginResponse::toTenant)
                                .toList());
        return new LoginResponse(
                result.email(), result.fullName(), result.phone(),
                session,
                tenantSelection);
    }

    private static Tenant toTenant(LoginUserResult.Tenant tenant) {
        return tenant == null
                ? null
                : new Tenant(
                        tenant.id(),
                        tenant.code(),
                        tenant.name(),
                        roleWireValue(tenant.role()));
    }

    private static int roleWireValue(String role) {
        if ("OWNER".equals(role)) {
            return 0;
        }
        throw new IllegalArgumentException("Unsupported tenant role: " + role);
    }

    public record Session(String accessToken, Instant expiresAt, Tenant tenant) {
    }

    public record Tenant(UUID id, String code, String name, int role) {
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
