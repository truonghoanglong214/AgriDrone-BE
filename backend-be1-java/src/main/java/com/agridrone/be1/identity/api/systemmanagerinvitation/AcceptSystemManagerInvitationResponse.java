package com.agridrone.be1.identity.api.systemmanagerinvitation;

import java.util.UUID;

public record AcceptSystemManagerInvitationResponse(
        UUID userId,
        UUID profileId,
        boolean accountCreated) {}
