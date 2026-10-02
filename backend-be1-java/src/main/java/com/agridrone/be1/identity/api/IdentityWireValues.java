package com.agridrone.be1.identity.api;

import com.agridrone.be1.identity.domain.TenantStatus;

public final class IdentityWireValues {
    private IdentityWireValues() {
    }

    public static int tenantStatus(TenantStatus status) {
        return switch (status) {
            case ACTIVE -> 0;
            case INACTIVE -> 1;
        };
    }

    public static int role(String role) {
        if ("OWNER".equals(role)) {
            return 0;
        }
        throw new IllegalArgumentException("Unsupported tenant role: " + role);
    }

    public static int membershipStatus(String status) {
        return switch (status) {
            case "ACTIVE" -> 0;
            case "INACTIVE" -> 1;
            default -> throw new IllegalArgumentException(
                    "Unsupported membership status: " + status);
        };
    }
}
