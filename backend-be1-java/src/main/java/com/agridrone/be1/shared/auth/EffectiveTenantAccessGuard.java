package com.agridrone.be1.shared.auth;

import java.util.UUID;

public interface EffectiveTenantAccessGuard {
    Decision evaluate(UUID userId, UUID tenantId, UUID membershipId);

    record Decision(boolean granted, String errorCode, String message) {
        public static Decision allow() {
            return new Decision(true, null, null);
        }

        public static Decision deny(String errorCode, String message) {
            return new Decision(false, errorCode, message);
        }
    }
}
