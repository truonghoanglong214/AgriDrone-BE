package com.agridrone.be1.identity.application.port.in.systemmanager;

import java.time.Instant;
import java.util.UUID;

public interface SystemManagerInvitationUseCase {
    InviteResult invite(String email);

    PreviewResult preview(String token);

    AcceptResult accept(AcceptCommand command);

    record InviteResult(
            UUID invitationId,
            String email,
            Instant expiresAt,
            boolean emailSent) {}

    record PreviewResult(
            String maskedEmail,
            String role,
            Instant expiresAt,
            boolean requiresAccountCreation) {}

    record AcceptCommand(String token, String password, String fullName, String phone) {}

    record AcceptResult(UUID userId, UUID profileId, boolean accountCreated) {}
}
