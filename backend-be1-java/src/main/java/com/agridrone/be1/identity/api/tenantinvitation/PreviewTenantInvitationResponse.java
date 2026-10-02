package com.agridrone.be1.identity.api.tenantinvitation;

import java.time.Instant;

public record PreviewTenantInvitationResponse(
        String maskedEmail,
        String tenantName,
        int role,
        Instant expiresAt,
        boolean requiresAccountCreation) {
}
