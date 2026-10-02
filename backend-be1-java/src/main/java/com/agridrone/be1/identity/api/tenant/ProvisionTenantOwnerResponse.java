package com.agridrone.be1.identity.api.tenant;

import java.time.Instant;
import java.util.UUID;

public record ProvisionTenantOwnerResponse(
        UUID invitationId,
        String email,
        Instant expiresAt) {
}
