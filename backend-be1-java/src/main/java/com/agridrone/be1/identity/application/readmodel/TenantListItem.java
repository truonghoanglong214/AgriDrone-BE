package com.agridrone.be1.identity.application.readmodel;

import com.agridrone.be1.identity.domain.TenantStatus;
import java.time.Instant;
import java.util.UUID;

public record TenantListItem(
        UUID id,
        String code,
        String name,
        TenantStatus status,
        Instant createdAt) {
}
