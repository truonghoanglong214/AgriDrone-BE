package com.agridrone.be1.identity.api.systemmanager;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

public record AssignedFarmResponse(
        UUID tenantId,
        UUID farmId,
        String code,
        String name,
        String address,
        BigDecimal areaHectares,
        Instant assignedAt) {}
