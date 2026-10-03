package com.agridrone.be1.farm.api.contract;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.UUID;

public record FarmListItemResponse(
        UUID id,
        UUID tenantId,
        String code,
        String name,
        String address,
        GeoJsonPolygonResponse boundary,
        GeoJsonPointResponse centerPoint,
        BigDecimal areaHectares,
        int status,
        Instant createdAt,
        UUID createdBy) {}
