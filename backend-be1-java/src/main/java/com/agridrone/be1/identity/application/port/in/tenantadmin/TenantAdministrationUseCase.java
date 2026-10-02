package com.agridrone.be1.identity.application.port.in.tenantadmin;

import com.agridrone.be1.identity.domain.TenantStatus;
import java.time.Instant;
import java.util.UUID;

public interface TenantAdministrationUseCase {
    CreateTenantResult create(CreateTenantCommand command);

    void activate(UUID tenantId);

    void deactivate(UUID tenantId);

    record CreateTenantCommand(String code, String name) {
    }

    record CreateTenantResult(
            UUID tenantId,
            String code,
            String name,
            TenantStatus status,
            Instant createdAt) {
    }
}
