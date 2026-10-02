package com.agridrone.be1.identity.application.port.in.tenantinvitation;

import java.time.Instant;
import java.util.UUID;

public interface TenantInvitationUseCase {
    ProvisionResult provisionOwner(UUID tenantId, String email);

    PreviewResult preview(String token);

    AcceptResult accept(AcceptCommand command);

    record ProvisionResult(UUID invitationId, String email, Instant expiresAt) {
    }

    record PreviewResult(
            String maskedEmail,
            String tenantName,
            String role,
            Instant expiresAt,
            boolean requiresAccountCreation) {
    }

    record AcceptCommand(String token, String password, String fullName, String phone) {
    }

    record AcceptResult(
            UUID userId,
            UUID tenantId,
            String role,
            boolean accountCreated) {
    }
}
