package com.agridrone.be1.identity.api.systemmanagerinvitation;

import java.time.Instant;

public record PreviewSystemManagerInvitationResponse(
        String maskedEmail,
        String role,
        Instant expiresAt,
        boolean requiresAccountCreation) {}
