package com.agridrone.be1.identity.api.auth.selection;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.util.UUID;

public record SelectTenantRequest(
        @NotBlank @Size(max = 2048) String selectionToken,
        @NotNull UUID tenantId) {
}
