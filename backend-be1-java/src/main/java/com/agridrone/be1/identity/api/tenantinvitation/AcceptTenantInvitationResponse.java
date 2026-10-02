package com.agridrone.be1.identity.api.tenantinvitation;

import java.util.UUID;

public record AcceptTenantInvitationResponse(
        UUID userId,
        UUID tenantId,
        int role,
        boolean accountCreated) {
}
