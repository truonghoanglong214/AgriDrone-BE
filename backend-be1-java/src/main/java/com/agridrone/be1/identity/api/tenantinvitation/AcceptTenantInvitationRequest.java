package com.agridrone.be1.identity.api.tenantinvitation;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;
import jakarta.validation.constraints.Size;

public record AcceptTenantInvitationRequest(
        @NotBlank @Size(max = 512) String token,
        @Size(min = 8, max = 128) String password,
        @Pattern(regexp = ".*\\S.*") @Size(max = 150) String fullName,
        @Size(max = 30) String phone) {
}
