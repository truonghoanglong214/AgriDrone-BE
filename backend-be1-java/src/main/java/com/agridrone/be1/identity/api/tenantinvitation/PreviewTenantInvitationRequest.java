package com.agridrone.be1.identity.api.tenantinvitation;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record PreviewTenantInvitationRequest(
        @NotBlank @Size(max = 512) String token) {
}
