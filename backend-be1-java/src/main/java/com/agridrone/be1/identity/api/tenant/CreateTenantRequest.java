package com.agridrone.be1.identity.api.tenant;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public record CreateTenantRequest(
        @NotBlank @Size(max = 50) String tenantCode,
        @NotBlank @Size(max = 150) String tenantName) {
}
