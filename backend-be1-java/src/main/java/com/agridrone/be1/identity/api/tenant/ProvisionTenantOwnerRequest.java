package com.agridrone.be1.identity.api.tenant;

import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record ProvisionTenantOwnerRequest(
        @NotBlank @Email @Size(max = 320) String email) {
}
