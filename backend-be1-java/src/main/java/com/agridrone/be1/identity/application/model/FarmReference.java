package com.agridrone.be1.identity.application.model;

import java.math.BigDecimal;
import java.util.UUID;

public record FarmReference(
        UUID tenantId,
        UUID farmId,
        String code,
        String name,
        String address,
        BigDecimal areaHectares) {}
