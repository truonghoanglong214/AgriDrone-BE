package com.agridrone.be1.identity.api.systemmanager;

import java.time.Instant;
import java.util.UUID;

public record InviteSystemManagerResponse(
        UUID invitationId,
        String email,
        Instant expiresAt,
        boolean emailSent) {}
